from django.contrib import admin
from django.contrib.auth import get_user_model

from crm_core.admin import CrmModelAdmin
from crm_core.services import record_event
from activities.models import Activity


@admin.register(Activity)
class ActivityAdmin(CrmModelAdmin):
    list_display = ("activity_date", "activity_type", "subject", "status", "assigned_to", "customer", "lead")
    list_filter = ("activity_type", "status", "activity_date")
    search_fields = ("subject", "description", "customer__customer_name", "lead__lead_name", "assigned_to__username")
    ordering = ("activity_date",)
    readonly_fields = ("status", "created_at", "created_by")

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
        if "assigned_to" in form.changed_data:
            record_event(actor=request.user, event_type="assigned", instance=obj)
        record_event(actor=request.user, event_type="created" if adding else "updated", instance=obj)