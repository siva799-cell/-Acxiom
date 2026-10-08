from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group
from django.core.exceptions import ValidationError
from django.test import TestCase
from django.urls import reverse

from accounts.roles import ADMIN, MANAGER, SALES_EXECUTIVE
from crm_core.models import AuditEvent
from customers.models import Customer
from leads.forms import LeadForm
from leads.models import Lead
from leads.services import convert_lead, transition_lead


class LeadManagementTests(TestCase):
    def setUp(self):
        user_model = get_user_model()
        self.admin = user_model.objects.create_user(username="lead-admin", password="GoodPassphrase!246")
        self.manager = user_model.objects.create_user(username="lead-manager", password="GoodPassphrase!246")
        self.sales = user_model.objects.create_user(username="lead-sales", password="GoodPassphrase!246")
        self.other_sales = user_model.objects.create_user(username="lead-other", password="GoodPassphrase!246")
        for user, role in ((self.admin, ADMIN), (self.manager, MANAGER), (self.sales, SALES_EXECUTIVE), (self.other_sales, SALES_EXECUTIVE)):
            group, _ = Group.objects.get_or_create(name=role)
            user.groups.add(group)

    def create_lead(self, *, name="Prospect", owner=None, status=Lead.Status.NEW, **overrides):
        owner = owner or self.sales
        values = {
            "lead_name": name,
            "email": f"{name.lower().replace(' ', '.')}@example.com",
            "phone": "15550001111",
            "company_name": "Prospect Co",
            "status": status,
            "source": Lead.Source.WEBSITE,
            "priority": Lead.Priority.MEDIUM,
            "expected_value": "1500.00",
            "assigned_to": owner,
            "created_by": owner,
        }
        values.update(overrides)
        return Lead.objects.create(**values)

    def test_create_generates_code_and_assigns_to_sales_user(self):
        self.client.force_login(self.manager)
        response = self.client.post(reverse("leads:create"), {
            "lead_name": "Fabrikam Prospect",
            "email": "SALES@fabrikam.example",
            "phone": "+1 555 222 9999",
            "company_name": "Fabrikam",
            "source": "Referral",
            "status": "New",
            "priority": "High",
            "expected_value": "2500.25",
            "assigned_to": self.sales.pk,
        })
        lead = Lead.objects.get(lead_name="Fabrikam Prospect")
        self.assertEqual(lead.lead_code, f"LEAD-{lead.pk:06d}")
        self.assertEqual(lead.assigned_to, self.sales)
        self.assertEqual(lead.email, "sales@fabrikam.example")
        self.assertRedirects(response, reverse("leads:detail", args=(lead.pk,)))
        self.assertEqual(self.client.get(reverse("leads:detail", args=(lead.pk,))).status_code, 200)

    def test_invalid_value_duplicate_and_unpermitted_assignment_are_rejected(self):
        self.create_lead(name="Existing Prospect")
        payload = {
            "lead_name": "Different Prospect",
            "email": "new@example.com",
            "phone": "15559990000",
            "company_name": "Other",
            "source": "Website",
            "status": "New",
            "priority": "Low",
            "expected_value": "-1",
            "assigned_to": self.sales.pk,
        }
        form = LeadForm(data=payload, user=self.manager)
        self.assertFalse(form.is_valid())
        self.assertIn("expected_value", form.errors)

        payload["expected_value"] = "0"
        payload["assigned_to"] = self.other_sales.pk
        self.assertFalse(LeadForm(data=payload, user=self.sales).is_valid())

    def test_status_transitions_reject_skips(self):
        lead = self.create_lead()
        with self.assertRaises(ValidationError):
            transition_lead(lead=lead, actor=self.sales, status=Lead.Status.QUALIFIED)
        transition_lead(lead=lead, actor=self.sales, status=Lead.Status.CONTACTED)
        lead.refresh_from_db()
        transition_lead(lead=lead, actor=self.sales, status=Lead.Status.QUALIFIED)
        lead.refresh_from_db()
        self.assertEqual(lead.status, Lead.Status.QUALIFIED)

    def test_edit_updates_lead_and_records_audit(self):
        lead = self.create_lead()
        self.client.force_login(self.sales)
        response = self.client.post(reverse("leads:edit", args=(lead.pk,)), {
            "lead_name": "Prospect Updated",
            "email": "updated@example.com",
            "phone": "15558887777",
            "company_name": lead.company_name,
            "source": lead.source,
            "status": Lead.Status.NEW,
            "priority": Lead.Priority.HIGH,
            "expected_value": "2000.00",
            "notes": "Updated notes",
            "assigned_to": self.sales.pk,
        })
        self.assertRedirects(response, reverse("leads:detail", args=(lead.pk,)))
        lead.refresh_from_db()
        self.assertEqual(lead.lead_name, "Prospect Updated")
        self.assertEqual(lead.priority, Lead.Priority.HIGH)
        self.assertTrue(AuditEvent.objects.filter(event_type="updated", object_id=str(lead.pk)).exists())

    def test_conversion_creates_customer_links_lead_and_audits_atomically(self):
        lead = self.create_lead(status=Lead.Status.QUALIFIED)
        self.client.force_login(self.sales)
        response = self.client.post(reverse("leads:convert", args=(lead.pk,)))
        lead.refresh_from_db()
        self.assertEqual(lead.status, Lead.Status.CONVERTED)
        self.assertEqual(lead.customer.customer_name, lead.lead_name)
        self.assertRedirects(response, reverse("customers:detail", args=(lead.customer_id,)))
        self.assertTrue(AuditEvent.objects.filter(event_type="converted", object_id=str(lead.pk)).exists())

    def test_conversion_reuses_matching_customer(self):
        customer = Customer.objects.create(
            customer_name="Existing Company",
            email="match@example.com",
            phone="15550002222",
            created_by=self.sales,
        )
        lead = self.create_lead(status=Lead.Status.QUALIFIED, email="MATCH@example.com", phone="15550003333")
        result = convert_lead(lead=lead, actor=self.sales)
        lead.refresh_from_db()
        self.assertEqual(result.pk, customer.pk)
        self.assertEqual(lead.customer_id, customer.pk)
        self.assertEqual(Customer.objects.count(), 1)

    def test_conversion_failure_rolls_back_customer_and_status(self):
        lead = self.create_lead(status=Lead.Status.QUALIFIED)
        with patch("leads.services.record_event", side_effect=RuntimeError("audit unavailable")):
            with self.assertRaises(RuntimeError):
                convert_lead(lead=lead, actor=self.sales)
        lead.refresh_from_db()
        self.assertEqual(lead.status, Lead.Status.QUALIFIED)
        self.assertIsNone(lead.customer_id)
        self.assertEqual(Customer.objects.count(), 0)

    def test_search_filters_pagination_and_assignment(self):
        for index in range(17):
            self.create_lead(
                name=f"Qualified Prospect {index}",
                email=f"qualified{index}@example.com",
                phone=f"1555000{index:04d}",
                status=Lead.Status.QUALIFIED,
                priority=Lead.Priority.HIGH,
            )
        target = self.create_lead(name="Rare Prospect", email="rare@example.com", phone="15559998888", status=Lead.Status.LOST)
        self.client.force_login(self.admin)
        response = self.client.get(reverse("leads:list"), {"q": "Rare", "status": Lead.Status.LOST})
        self.assertEqual(list(response.context["page_obj"].object_list), [target])
        response = self.client.get(reverse("leads:list"), {"page": 2, "priority": Lead.Priority.HIGH, "assigned_to": self.sales.pk})
        self.assertEqual(response.context["page_obj"].number, 2)
        self.assertEqual(response.context["page_obj"].paginator.count, 17)

    def test_sales_user_cannot_view_another_users_lead_by_id(self):
        lead = self.create_lead(owner=self.sales)
        self.client.force_login(self.other_sales)
        self.assertEqual(self.client.get(reverse("leads:detail", args=(lead.pk,))).status_code, 404)
        self.assertEqual(self.client.post(reverse("leads:convert", args=(lead.pk,))).status_code, 404)