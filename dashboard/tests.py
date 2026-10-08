from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group
from django.test import TestCase
from django.urls import reverse

from accounts.roles import ADMIN, MANAGER, SALES_EXECUTIVE


class DashboardAccessTests(TestCase):
    def test_anonymous_user_is_redirected_to_login(self):
        response = self.client.get(reverse("dashboard:home"))
        self.assertRedirects(response, f"{reverse('accounts:login')}?next={reverse('dashboard:home')}")

    def test_authenticated_user_sees_real_profile_without_fake_metrics(self):
        user = get_user_model().objects.create_user(
            username="siva",
            first_name="Siva",
            email="sales@acxiomcrm.local",
            password="GoodPassphrase!246",
        )
        group, _ = Group.objects.get_or_create(name=SALES_EXECUTIVE)
        user.groups.add(group)
        self.client.force_login(user)

        response = self.client.get(reverse("dashboard:home"))

        self.assertContains(response, "Welcome back, Siva")
        self.assertContains(response, "sales@acxiomcrm.local")
        self.assertContains(response, "SalesExecutive")
        self.assertContains(response, "You have access to your assigned CRM activities.")
        self.assertNotContains(response, "1,250")
        self.assertNotContains(response, "20,000,000")

    def test_role_navigation_exposes_admin_items_only_to_admin(self):
        for role in (ADMIN, MANAGER, SALES_EXECUTIVE):
            user = get_user_model().objects.create_user(
                username=role,
                password="GoodPassphrase!246",
            )
            group, _ = Group.objects.get_or_create(name=role)
            user.groups.add(group)
            self.client.force_login(user)
            response = self.client.get(reverse("dashboard:home"))
            self.assertContains(response, "Customers")
            self.assertContains(response, "Dashboard")
            self.assertContains(response, "aria-disabled=\"true\"")
            if role == ADMIN:
                self.assertContains(response, "User Management")
                self.assertContains(response, "Role Management")
            else:
                self.assertNotContains(response, "User Management")
                self.assertNotContains(response, "Audit Logs")