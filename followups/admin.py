from django.contrib import admin
from django.contrib.auth import get_user_model

from crm_core.admin import CrmModelAdmin
from crm_core.services import record_event
from followups.models import FollowUp


@admin.register(FollowUp)
class FollowUpAdmin(CrmModelAdmin):
    list_display = ("follow_up_date", "subject", "follow_up_type", "status", "assigned_to", "customer", "lead")
    list_filter = ("status", "follow_up_type", "follow_up_date")
    search_fields = ("subject", "remarks", "customer__customer_name", "lead__lead_name", "assigned_to__username")
    ordering = ("follow_up_date",)
    readonly_fields = ("status", "completed_at", "created_at", "created_by")

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
        event_type = "created" if adding else "updated"
        if "follow_up_date" in form.changed_data:
            record_event(actor=request.user, event_type="rescheduled", instance=obj)
        if "assigned_to" in form.changed_data:
            record_event(actor=request.user, event_type="assigned", instance=obj)
        record_event(actor=request.user, event_type=event_type, instance=obj)