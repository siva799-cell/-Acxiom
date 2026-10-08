from django.conf import settings
from django.contrib.contenttypes.fields import GenericForeignKey
from django.contrib.contenttypes.models import ContentType
from django.core.exceptions import ValidationError
from django.db import models
from django.db.models import Q


class Activity(models.Model):
    class Type(models.TextChoices):
        CALL = "Call", "Call"
        MEETING = "Meeting", "Meeting"
        EMAIL = "Email", "Email"
        TASK = "Task", "Task"

    class Status(models.TextChoices):
        PLANNED = "Planned", "Planned"
        COMPLETED = "Completed", "Completed"
        CANCELLED = "Cancelled", "Cancelled"

    activity_type = models.CharField(max_length=10, choices=Type.choices)
    subject = models.CharField(max_length=180)
    description = models.TextField()
    activity_date = models.DateField(db_index=True)
    customer = models.ForeignKey("customers.Customer", null=True, blank=True, on_delete=models.PROTECT, related_name="activities")
    lead = models.ForeignKey("leads.Lead", null=True, blank=True, on_delete=models.PROTECT, related_name="activities")
    opportunity_content_type = models.ForeignKey(ContentType, null=True, blank=True, on_delete=models.PROTECT)
    opportunity_object_id = models.PositiveBigIntegerField(null=True, blank=True)
    opportunity = GenericForeignKey("opportunity_content_type", "opportunity_object_id")
    assigned_to = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT, related_name="assigned_activities", db_index=True)
    status = models.CharField(max_length=10, choices=Status.choices, default=Status.PLANNED, db_index=True)
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT, related_name="created_activities")

    class Meta:
        ordering = ("activity_date", "pk")
        constraints = [
            models.CheckConstraint(
                condition=(
                    Q(customer__isnull=False, lead__isnull=True, opportunity_content_type__isnull=True, opportunity_object_id__isnull=True)
                    | Q(customer__isnull=True, lead__isnull=False, opportunity_content_type__isnull=True, opportunity_object_id__isnull=True)
                    | Q(customer__isnull=True, lead__isnull=True, opportunity_content_type__isnull=False, opportunity_object_id__isnull=False)
                ),
                name="activity_has_one_related_record",
            )
        ]
        indexes = [models.Index(fields=("assigned_to", "activity_date")), models.Index(fields=("status", "activity_date")), models.Index(fields=("activity_type", "activity_date"))]

    def clean(self):
        super().clean()
        related_count = sum((bool(self.customer_id), bool(self.lead_id), bool(self.opportunity_content_type_id and self.opportunity_object_id)))
        if related_count != 1:
            raise ValidationError("Select exactly one customer, lead, or opportunity.")

    def __str__(self):
        return self.subject