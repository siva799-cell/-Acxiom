from datetime import timedelta

from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group
from django.core.exceptions import ValidationError
from django.test import TestCase
from django.urls import reverse
from django.utils import timezone

from accounts.roles import MANAGER, SALES_EXECUTIVE
from activities.forms import ActivityForm
from activities.models import Activity
from activities.services import transition_activity
from customers.models import Customer
from leads.models import Lead


class ActivityManagementTests(TestCase):
    def setUp(self):
        user_model = get_user_model()
        self.manager = user_model.objects.create_user(username="activity-manager", password="GoodPassphrase!246")
        self.sales = user_model.objects.create_user(username="activity-sales", password="GoodPassphrase!246")
        self.other_sales = user_model.objects.create_user(username="activity-other", password="GoodPassphrase!246")
        for user, role in ((self.manager, MANAGER), (self.sales, SALES_EXECUTIVE), (self.other_sales, SALES_EXECUTIVE)):
            group, _ = Group.objects.get_or_create(name=role)
            user.groups.add(group)
        self.customer = Customer.objects.create(customer_name="Activity Customer", created_by=self.sales)
        self.lead = Lead.objects.create(lead_name="Activity Lead", assigned_to=self.sales, created_by=self.sales)

    def create_activity(self, *, subject="Kickoff call", owner=None, customer=None, date=None):
        owner = owner or self.sales
        return Activity.objects.create(
            activity_type=Activity.Type.CALL,
            subject=subject,
            description="Discuss the account requirements.",
            activity_date=date or timezone.localdate(),
            customer=self.customer if customer is None else customer,
            assigned_to=owner,
            status=Activity.Status.PLANNED,
            created_by=owner,
        )

    def test_activity_create_and_required_fields(self):
        self.client.force_login(self.sales)
        response = self.client.post(reverse("activities:create"), {
            "activity_type": "Meeting",
            "subject": "Discovery meeting",
            "description": "Review the prospect needs.",
            "activity_date": timezone.localdate().isoformat(),
            "customer": self.customer.pk,
            "lead": "",
            "assigned_to": self.sales.pk,
        })
        activity = Activity.objects.get(subject="Discovery meeting")
        self.assertEqual(activity.created_by, self.sales)
        self.assertRedirects(response, reverse("activities:detail", args=(activity.pk,)))
        self.assertEqual(self.client.get(reverse("activities:detail", args=(activity.pk,))).status_code, 200)

        form = ActivityForm(data={
            "activity_type": "Call",
            "subject": "No relation",
            "description": "Description",
            "activity_date": timezone.localdate().isoformat(),
            "customer": "",
            "lead": "",
            "assigned_to": self.sales.pk,
        }, user=self.sales)
        self.assertFalse(form.is_valid())
        self.assertTrue(form.non_field_errors())

    def test_activity_status_transitions(self):
        activity = self.create_activity()
        transition_activity(activity=activity, actor=self.sales, status=Activity.Status.COMPLETED)
        activity.refresh_from_db()
        self.assertEqual(activity.status, Activity.Status.COMPLETED)
        with self.assertRaises(ValidationError):
            transition_activity(activity=activity, actor=self.sales, status=Activity.Status.CANCELLED)

    def test_edit_updates_activity(self):
        activity = self.create_activity()
        self.client.force_login(self.sales)
        response = self.client.post(reverse("activities:edit", args=(activity.pk,)), {
            "activity_type": Activity.Type.MEETING,
            "subject": "Updated activity",
            "description": "Updated description",
            "activity_date": (timezone.localdate() + timedelta(days=1)).isoformat(),
            "customer": self.customer.pk,
            "lead": "",
            "assigned_to": self.sales.pk,
        })
        self.assertRedirects(response, reverse("activities:detail", args=(activity.pk,)))
        activity.refresh_from_db()
        self.assertEqual(activity.subject, "Updated activity")
        self.assertEqual(activity.activity_type, Activity.Type.MEETING)

    def test_filtering_and_pagination(self):
        today = timezone.localdate()
        self.create_activity(subject="Today call", date=today)
        for index in range(16):
            self.create_activity(subject=f"Future task {index}", date=today + timedelta(days=2))
        self.client.force_login(self.manager)
        response = self.client.get(reverse("activities:list"), {"activity_type": "Call", "date_from": today.isoformat(), "date_to": today.isoformat()})
        self.assertEqual(response.context["page_obj"].paginator.count, 1)
        response = self.client.get(reverse("activities:list"), {"page": 2, "status": "Planned"})
        self.assertEqual(response.context["page_obj"].number, 2)
        self.assertEqual(response.context["page_obj"].paginator.count, 17)

    def test_sales_user_cannot_view_other_users_activity_or_assign_to_peer(self):
        activity = self.create_activity()
        self.client.force_login(self.other_sales)
        self.assertEqual(self.client.get(reverse("activities:detail", args=(activity.pk,))).status_code, 404)
        form = ActivityForm(data={
            "activity_type": "Task",
            "subject": "Unauthorized assignment",
            "description": "No",
            "activity_date": timezone.localdate().isoformat(),
            "customer": self.customer.pk,
            "lead": "",
            "assigned_to": self.other_sales.pk,
        }, user=self.sales)
        self.assertFalse(form.is_valid())

    def test_invalid_date_range_is_rejected(self):
        self.client.force_login(self.manager)
        response = self.client.get(reverse("activities:list"), {
            "date_from": (timezone.localdate() + timedelta(days=3)).isoformat(),
            "date_to": timezone.localdate().isoformat(),
        })
        self.assertFalse(response.context["filter_form"].is_valid())