from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group
from django.test import Client
from django.test import TestCase
from django.urls import reverse

from accounts.roles import ADMIN, SALES_EXECUTIVE
from crm_core.models import AuditEvent
from customers.forms import CustomerForm
from customers.models import Customer


class CustomerManagementTests(TestCase):
    def setUp(self):
        user_model = get_user_model()
        self.admin = user_model.objects.create_user(username="customer-admin", password="GoodPassphrase!246")
        self.sales = user_model.objects.create_user(username="customer-sales", password="GoodPassphrase!246")
        self.other_sales = user_model.objects.create_user(username="other-sales", password="GoodPassphrase!246")
        for user, role in ((self.admin, ADMIN), (self.sales, SALES_EXECUTIVE), (self.other_sales, SALES_EXECUTIVE)):
            group, _ = Group.objects.get_or_create(name=role)
            user.groups.add(group)

    def create_customer(self, *, name="Northwind", owner=None, **overrides):
        values = {
            "customer_name": name,
            "email": "contact@example.com",
            "phone": "+15551234567",
            "company_name": "Northwind Ltd",
            "status": Customer.Status.ACTIVE,
            "created_by": owner or self.sales,
        }
        values.update(overrides)
        return Customer.objects.create(**values)

    def test_create_generates_code_and_audit_event(self):
        self.client.force_login(self.sales)
        response = self.client.post(reverse("customers:create"), {
            "customer_name": "Contoso",
            "email": "HELLO@contoso.example",
            "phone": "+1 (555) 222-3333",
            "company_name": "Contoso",
            "status": "Active",
        })
        customer = Customer.objects.get(customer_name="Contoso")
        self.assertEqual(customer.customer_code, f"CUS-{customer.pk:06d}")
        self.assertEqual(customer.email, "hello@contoso.example")
        self.assertRedirects(response, reverse("customers:detail", args=(customer.pk,)))
        self.assertEqual(self.client.get(reverse("customers:detail", args=(customer.pk,))).status_code, 200)
        self.assertTrue(AuditEvent.objects.filter(event_type="created", object_id=str(customer.pk)).exists())

    def test_duplicate_customer_is_rejected_and_phone_normalized(self):
        self.create_customer()
        form = CustomerForm(data={
            "customer_name": "Another Name",
            "email": "CONTACT@example.com",
            "phone": "(555) 123-4567",
            "company_name": "Other",
            "status": "Active",
        })
        self.assertFalse(form.is_valid())
        self.assertIn("already exists", str(form.non_field_errors()))

        valid = CustomerForm(data={
            "customer_name": "Another Name",
            "email": "different@example.com",
            "phone": "(555) 999-0000",
            "company_name": "Other",
            "status": "Active",
        })
        self.assertTrue(valid.is_valid(), valid.errors)
        self.assertEqual(valid.cleaned_data["phone"], "5559990000")

    def test_customer_status_changes_are_post_only_and_audited(self):
        customer = self.create_customer()
        self.client.force_login(self.sales)
        url = reverse("customers:toggle_status", args=(customer.pk,))
        self.assertEqual(self.client.get(url).status_code, 405)
        response = self.client.post(url)
        customer.refresh_from_db()
        self.assertEqual(customer.status, Customer.Status.INACTIVE)
        self.assertRedirects(response, reverse("customers:detail", args=(customer.pk,)))
        self.assertTrue(AuditEvent.objects.filter(event_type="deactivated", object_id=str(customer.pk)).exists())

    def test_customer_edit_and_csrf_protection(self):
        customer = self.create_customer(email="edit@example.com", phone="15551112222")
        self.client.force_login(self.sales)
        response = self.client.post(reverse("customers:edit", args=(customer.pk,)), {
            "customer_name": customer.customer_name,
            "email": customer.email,
            "phone": customer.phone,
            "company_name": "Updated Company",
            "address": "Updated address",
            "city": "Austin",
            "state": "Texas",
            "status": Customer.Status.ACTIVE,
        })
        self.assertRedirects(response, reverse("customers:detail", args=(customer.pk,)))
        customer.refresh_from_db()
        self.assertEqual(customer.company_name, "Updated Company")
        self.assertTrue(AuditEvent.objects.filter(event_type="updated", object_id=str(customer.pk)).exists())

        csrf_client = Client(enforce_csrf_checks=True)
        csrf_client.force_login(self.sales)
        rejected = csrf_client.post(reverse("customers:toggle_status", args=(customer.pk,)))
        self.assertEqual(rejected.status_code, 403)
        customer.refresh_from_db()
        self.assertEqual(customer.status, Customer.Status.ACTIVE)

    def test_search_status_filter_and_pagination(self):
        for index in range(17):
            self.create_customer(name=f"Customer {index}", email=f"person{index}@example.com", phone=f"1555000{index:04d}")
        inactive = self.create_customer(name="Inactive Target", email="inactive@example.com", phone="15559990000", status=Customer.Status.INACTIVE)
        self.client.force_login(self.admin)
        response = self.client.get(reverse("customers:list"), {"q": "Target", "status": "Inactive"})
        self.assertEqual(list(response.context["page_obj"].object_list), [inactive])
        response = self.client.get(reverse("customers:list"), {"page": 2, "status": "Active"})
        self.assertEqual(response.context["page_obj"].number, 2)
        self.assertEqual(response.context["page_obj"].paginator.count, 17)

    def test_sales_user_cannot_read_or_modify_another_users_customer(self):
        customer = self.create_customer(owner=self.sales)
        self.client.force_login(self.other_sales)
        for url in (reverse("customers:detail", args=(customer.pk,)), reverse("customers:edit", args=(customer.pk,))):
            self.assertEqual(self.client.get(url).status_code, 404)
        self.assertEqual(self.client.post(reverse("customers:toggle_status", args=(customer.pk,))).status_code, 404)
        customer.refresh_from_db()
        self.assertEqual(customer.status, Customer.Status.ACTIVE)

    def test_unassigned_authenticated_user_cannot_create_customer(self):
        user = get_user_model().objects.create_user(username="no-crm-role", password="GoodPassphrase!246")
        self.client.force_login(user)
        self.assertEqual(self.client.get(reverse("customers:create")).status_code, 403)