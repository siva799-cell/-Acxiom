from django.contrib import admin
from django.contrib.auth import get_user_model
from django.contrib.auth.models import Group
from django.test import RequestFactory, TestCase

from accounts.roles import ADMIN, MANAGER, SALES_EXECUTIVE
from activities.models import Activity
from customers.models import Customer
from followups.models import FollowUp
from leads.models import Lead


class CrmAdminAuthorizationTests(TestCase):
    def setUp(self):
        user_model = get_user_model()
        self.admin_user = user_model.objects.create_user(username="crm-admin", is_staff=True, password="GoodPassphrase!246")
        self.manager_user = user_model.objects.create_user(username="crm-manager", is_staff=True, password="GoodPassphrase!246")
        self.sales_user = user_model.objects.create_user(username="crm-sales", is_staff=True, password="GoodPassphrase!246")
        for user, role in ((self.admin_user, ADMIN), (self.manager_user, MANAGER), (self.sales_user, SALES_EXECUTIVE)):
            group, _ = Group.objects.get_or_create(name=role)
            user.groups.add(group)
        self.factory = RequestFactory()

    def test_only_admin_role_can_access_crm_admin_and_no_admin_delete(self):
        for model in (Customer, Lead, FollowUp, Activity):
            model_admin = admin.site._registry[model]
            admin_request = self.factory.get("/admin/")
            admin_request.user = self.admin_user
            manager_request = self.factory.get("/admin/")
            manager_request.user = self.manager_user
            sales_request = self.factory.get("/admin/")
            sales_request.user = self.sales_user
            self.assertTrue(model_admin.has_module_permission(admin_request))
            self.assertFalse(model_admin.has_module_permission(manager_request))
            self.assertFalse(model_admin.has_module_permission(sales_request))
            self.assertFalse(model_admin.has_delete_permission(admin_request))

    def test_workflow_statuses_are_read_only_in_admin(self):
        for model in (Lead, FollowUp, Activity):
            model_admin = admin.site._registry[model]
            request = self.factory.get("/admin/")
            request.user = self.admin_user
            self.assertIn("status", model_admin.get_readonly_fields(request))