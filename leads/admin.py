from django.contrib import admin
from django.contrib.auth import get_user_model

from crm_core.admin import CrmModelAdmin
from crm_core.services import record_event
from leads.models import Lead


@admin.register(Lead)
class LeadAdmin(CrmModelAdmin):
    list_display = ("lead_code", "lead_name", "company_name", "source", "status", "priority", "assigned_to", "created_at")
    list_filter = ("status", "source", "priority", "created_at")
    search_fields = ("lead_code", "lead_name", "company_name", "email", "phone")
    ordering = ("-created_at",)
    readonly_fields = ("lead_code", "created_at", "modified_at", "created_by", "customer", "status")

    def formfield_for_foreignkey(self, db_field, request, **kwargs):
        if db_field.name == "assigned_to":
            kwargs["queryset"] = get_user_model().objects.filter(
                is_active=True,
                groups__name="SalesExecutive",
            ).distinct().order_by("username")
        return super().formfield_for_foreignkey(db_field, request, **kwargs)

    def save_model(self, request, obj, form, change):
        adding = not change
        if adding:
            obj.created_by = request.user
        super().save_model(request, obj, form, change)
        if adding:
            event_type = "created"
        elif "assigned_to" in form.changed_data:
            record_event(actor=request.user, event_type="assigned", instance=obj)
            event_type = "updated"
        else:
            event_type = "updated"
        record_event(actor=request.user, event_type=event_type, instance=obj)