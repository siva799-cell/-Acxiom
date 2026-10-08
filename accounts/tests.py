from django.contrib.auth import authenticate, get_user_model
from django.contrib.auth.models import Group
from django.core.exceptions import PermissionDenied
from django.core.cache import cache
from django.http import HttpResponse
from django.test import RequestFactory, TestCase
from django.urls import reverse

from accounts.roles import ADMIN, MANAGER, SALES_EXECUTIVE, get_user_role, role_required


class RoleTests(TestCase):
    def setUp(self):
        self.user = get_user_model().objects.create_user(username="role-user", password="GoodPassphrase!246")

    def test_group_roles_are_resolved(self):
        for role in (ADMIN, MANAGER, SALES_EXECUTIVE):
            group, _ = Group.objects.get_or_create(name=role)
            self.user.groups.set([group])
            self.assertEqual(get_user_role(self.user), role)

    def test_superuser_is_admin(self):
        self.user.is_superuser = True
        self.user.save(update_fields=["is_superuser"])
        self.assertEqual(get_user_role(self.user), ADMIN)


class LockoutBackendTests(TestCase):
    def setUp(self):
        cache.clear()
        self.username = "lockout-user"
        get_user_model().objects.create_user(username=self.username, password="GoodPassphrase!246")
        self.request = RequestFactory().post("/accounts/login/", REMOTE_ADDR="127.0.0.1")

    def tearDown(self):
        cache.clear()

    def test_five_failed_attempts_lock_account(self):
        for _ in range(5):
            self.assertIsNone(authenticate(self.request, username=self.username, password="incorrect"))
        self.assertIsNone(authenticate(self.request, username=self.username, password="GoodPassphrase!246"))

    def test_successful_authentication_resets_failures(self):
        authenticate(self.request, username=self.username, password="incorrect")
        user = authenticate(self.request, username=self.username, password="GoodPassphrase!246")
        self.assertEqual(user.username, self.username)


class AuthenticationFlowTests(TestCase):
    def setUp(self):
        self.password = "GoodPassphrase!246"
        self.user = get_user_model().objects.create_user(username="flow-user", password=self.password)

    def test_login_and_post_logout(self):
        login_response = self.client.post(
            reverse("accounts:login"),
            {"username": self.user.username, "password": self.password},
        )
        self.assertRedirects(login_response, reverse("dashboard:home"))
        self.assertEqual(self.client.get(reverse("dashboard:home")).status_code, 200)

        logout_response = self.client.post(reverse("accounts:logout"))
        self.assertRedirects(logout_response, reverse("accounts:login"))
        dashboard_response = self.client.get(reverse("dashboard:home"))
        self.assertRedirects(
            dashboard_response,
            f"{reverse('accounts:login')}?next={reverse('dashboard:home')}",
        )

    def test_login_and_logout_require_csrf(self):
        from django.test import Client

        client = Client(enforce_csrf_checks=True)
        client.get(reverse("accounts:login"))
        rejected_login = client.post(
            reverse("accounts:login"),
            {"username": self.user.username, "password": self.password},
        )
        self.assertEqual(rejected_login.status_code, 403)

        token = client.cookies["csrftoken"].value
        accepted_login = client.post(
            reverse("accounts:login"),
            {"username": self.user.username, "password": self.password},
            HTTP_X_CSRFTOKEN=token,
        )
        self.assertEqual(accepted_login.status_code, 302)
        rejected_logout = client.post(reverse("accounts:logout"))
        self.assertEqual(rejected_logout.status_code, 403)

    def test_role_required_enforces_server_side_access(self):
        manager_group, _ = Group.objects.get_or_create(name=MANAGER)
        self.user.groups.add(manager_group)

        @role_required(ADMIN)
        def admin_only(request):
            return HttpResponse("allowed")

        request = RequestFactory().get("/protected/")
        request.user = self.user
        with self.assertRaises(PermissionDenied):
            admin_only(request)