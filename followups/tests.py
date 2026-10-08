from datetime import timedelta

from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group
from django.core.exceptions import ValidationError
from django.test import TestCase
from django.urls import reverse
from django.utils import timezone

from accounts.roles import MANAGER, SALES_EXECUTIVE
from crm_core.models import AuditEvent
from customers.models import Customer
from followups.forms import FollowUpForm
from followups.models import FollowUp
from followups.services import transition_followup
from leads.models import Lead


class FollowUpManagementTests(TestCase):
    def setUp(self):
        user_model = get_user_model()
        self.manager = user_model.objects.create_user(username="follow-manager", password="GoodPassphrase!246")
        self.sales = user_model.objects.create_user(username="follow-sales", password="GoodPassphrase!246")
        self.other_sales = user_model.objects.create_user(username="follow-other", password="GoodPassphrase!246")
        for user, role in ((self.manager, MANAGER), (self.sales, SALES_EXECUTIVE), (self.other_sales, SALES_EXECUTIVE)):
            group, _ = Group.objects.get_or_create(name=role)
            user.groups.add(group)
        self.customer = Customer.objects.create(customer_name="Follow Customer", created_by=self.sales)
        self.lead = Lead.objects.create(lead_name="Follow Lead", assigned_to=self.sales, created_by=self.sales)

    def create_followup(self, *, subject="Call customer", owner=None, customer=None, date=None, status=FollowUp.Status.PLANNED):
        owner = owner or self.sales
        return FollowUp.objects.create(
            customer=self.customer if customer is None else customer,
            follow_up_date=date or timezone.localdate(),
            follow_up_type=FollowUp.Type.CALL,
            subject=subject,
            remarks="Discuss next steps",
            status=status,
            assigned_to=owner,
            created_by=owner,
        )

    def test_create_and_related_record_validation(self):
        self.client.force_login(self.sales)
        response = self.client.post(reverse("followups:create"), {
            "customer": self.customer.pk,
            "lead": "",
            "follow_up_date": timezone.localdate().isoformat(),
            "follow_up_type": "Call",
            "subject": "Confirm requirements",
            "remarks": "Call to discuss requirements",
            "assigned_to": self.sales.pk,
        })
        followup = FollowUp.objects.get(subject="Confirm requirements")
        self.assertEqual(followup.status, FollowUp.Status.PLANNED)
        self.assertEqual(followup.created_by, self.sales)
        self.assertRedirects(response, reverse("followups:detail", args=(followup.pk,)))
        self.assertEqual(self.client.get(reverse("followups:detail", args=(followup.pk,))).status_code, 200)
        self.assertTrue(AuditEvent.objects.filter(event_type="created", object_id=str(followup.pk)).exists())

        form = FollowUpForm(data={
            "customer": "",
            "lead": "",
            "follow_up_date": timezone.localdate().isoformat(),
            "follow_up_type": "Call",
            "subject": "Unlinked",
            "assigned_to": self.sales.pk,
        }, user=self.sales)
        self.assertFalse(form.is_valid())

    def test_new_planned_followup_cannot_be_backdated(self):
        form = FollowUpForm(data={
            "customer": self.customer.pk,
            "lead": "",
            "follow_up_date": (timezone.localdate() - timedelta(days=1)).isoformat(),
            "follow_up_type": "Meeting",
            "subject": "Past meeting",
            "assigned_to": self.sales.pk,
        }, user=self.sales)
        self.assertFalse(form.is_valid())
        self.assertIn("follow_up_date", form.errors)

    def test_status_transitions_preserve_date_and_set_completed_time(self):
        followup = self.create_followup(date=timezone.localdate() + timedelta(days=4))
        original_date = followup.follow_up_date
        transition_followup(followup=followup, actor=self.sales, status=FollowUp.Status.COMPLETED)
        followup.refresh_from_db()
        self.assertEqual(followup.follow_up_date, original_date)
        self.assertIsNotNone(followup.completed_at)
        with self.assertRaises(ValidationError):
            transition_followup(followup=followup, actor=self.sales, status=FollowUp.Status.CANCELLED)
        self.assertTrue(AuditEvent.objects.filter(event_type="completed", object_id=str(followup.pk)).exists())

    def test_edit_reschedules_and_keeps_history_event(self):
        followup = self.create_followup(date=timezone.localdate() + timedelta(days=1))
        new_date = timezone.localdate() + timedelta(days=5)
        self.client.force_login(self.sales)
        response = self.client.post(reverse("followups:edit", args=(followup.pk,)), {
            "customer": self.customer.pk,
            "lead": "",
            "follow_up_date": new_date.isoformat(),
            "follow_up_type": FollowUp.Type.MEETING,
            "subject": "Rescheduled meeting",
            "remarks": "New date confirmed",
            "assigned_to": self.sales.pk,
        })
        self.assertRedirects(response, reverse("followups:detail", args=(followup.pk,)))
        followup.refresh_from_db()
        self.assertEqual(followup.follow_up_date, new_date)
        self.assertTrue(AuditEvent.objects.filter(event_type="rescheduled", object_id=str(followup.pk)).exists())

    def test_overdue_is_derived_and_missed_requires_past_date(self):
        followup = self.create_followup(date=timezone.localdate() - timedelta(days=1))
        self.assertTrue(followup.is_overdue)
        transition_followup(followup=followup, actor=self.sales, status=FollowUp.Status.MISSED)
        followup.refresh_from_db()
        self.assertEqual(followup.status, FollowUp.Status.MISSED)
        self.assertFalse(followup.is_overdue)

    def test_filtering_and_pagination_are_server_side(self):
        today = timezone.localdate()
        self.create_followup(subject="Today item", date=today)
        for index in range(16):
            self.create_followup(subject=f"Future item {index}", date=today + timedelta(days=1))
        self.client.force_login(self.manager)
        response = self.client.get(reverse("followups:list"), {"period": "today", "status": "Planned"})
        self.assertEqual(response.context["page_obj"].paginator.count, 1)
        response = self.client.get(reverse("followups:list"), {"period": "", "page": 2})
        self.assertEqual(response.context["page_obj"].number, 2)
        self.assertEqual(response.context["page_obj"].paginator.count, 17)

    def test_sales_assignment_and_idor_are_restricted(self):
        followup = self.create_followup()
        self.client.force_login(self.other_sales)
        self.assertEqual(self.client.get(reverse("followups:detail", args=(followup.pk,))).status_code, 404)
        form = FollowUpForm(data={
            "customer": self.customer.pk,
            "lead": "",
            "follow_up_date": timezone.localdate().isoformat(),
            "follow_up_type": "Call",
            "subject": "Reassign attempt",
            "assigned_to": self.other_sales.pk,
        }, user=self.sales)
        self.assertFalse(form.is_valid())

    def test_future_followup_cannot_be_marked_missed(self):
        followup = self.create_followup(date=timezone.localdate() + timedelta(days=1))
        with self.assertRaises(ValidationError):
            transition_followup(followup=followup, actor=self.sales, status=FollowUp.Status.MISSED)