from django.contrib import admin

from crm_core.admin import CrmModelAdmin
from crm_core.services import record_event
from customers.models import Customer


@admin.register(Customer)
class CustomerAdmin(CrmModelAdmin):
    list_display = ("customer_code", "customer_name", "company_name", "email", "phone", "status", "created_at")
    list_filter = ("status", "state", "created_at")
    search_fields = ("customer_code", "customer_name", "company_name", "email", "phone")
    ordering = ("-created_at",)
    readonly_fields = ("customer_code", "created_at", "modified_at", "created_by")

    def save_model(self, request, obj, form, change):
        adding = not change
        if adding:
            obj.created_by = request.user
        super().save_model(request, obj, form, change)
        if adding:
            event_type = "created"
        elif "status" in form.changed_data:
            event_type = "activated" if obj.status == Customer.Status.ACTIVE else "deactivated"
        else:
            event_type = "updated"
        record_event(actor=request.user, event_type=event_type, instance=obj)