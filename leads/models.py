from decimal import Decimal

from django.conf import settings
from django.core.validators import MinValueValidator, RegexValidator
from django.db import models


class Lead(models.Model):
    class Status(models.TextChoices):
        NEW = "New", "New"
        CONTACTED = "Contacted", "Contacted"
        QUALIFIED = "Qualified", "Qualified"
        UNQUALIFIED = "Unqualified", "Unqualified"
        CONVERTED = "Converted", "Converted"
        LOST = "Lost", "Lost"

    class Source(models.TextChoices):
        WEBSITE = "Website", "Website"
        REFERRAL = "Referral", "Referral"
        SOCIAL_MEDIA = "Social Media", "Social Media"
        EMAIL = "Email", "Email"
        PHONE = "Phone", "Phone"
        ADVERTISEMENT = "Advertisement", "Advertisement"
        OTHER = "Other", "Other"

    class Priority(models.TextChoices):
        LOW = "Low", "Low"
        MEDIUM = "Medium", "Medium"
        HIGH = "High", "High"

    lead_code = models.CharField(max_length=18, unique=True, blank=True)
    lead_name = models.CharField(max_length=160)
    email = models.EmailField(blank=True)
    phone = models.CharField(max_length=16, blank=True, validators=[RegexValidator(r"^\+?[0-9]{7,15}$", "Enter a phone number containing 7 to 15 digits.")])
    company_name = models.CharField(max_length=160, blank=True)
    source = models.CharField(max_length=20, choices=Source.choices, default=Source.OTHER, db_index=True)
    status = models.CharField(max_length=12, choices=Status.choices, default=Status.NEW, db_index=True)
    priority = models.CharField(max_length=8, choices=Priority.choices, default=Priority.MEDIUM, db_index=True)
    expected_value = models.DecimalField(max_digits=14, decimal_places=2, default=Decimal("0.00"), validators=[MinValueValidator(0)])
    notes = models.TextField(blank=True)
    assigned_to = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT, related_name="assigned_leads")
    customer = models.ForeignKey("customers.Customer", null=True, blank=True, on_delete=models.PROTECT, related_name="converted_leads")
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT, related_name="created_leads")
    modified_at = models.DateTimeField(auto_now=True)

    class Meta:
        ordering = ("-created_at", "-pk")
        indexes = [models.Index(fields=("assigned_to", "status"))]

    def save(self, *args, **kwargs):
        if not self.lead_code:
            super().save(*args, **kwargs)
            self.lead_code = f"LEAD-{self.pk:06d}"
            type(self).objects.filter(pk=self.pk).update(lead_code=self.lead_code)
            return
        super().save(*args, **kwargs)

    def __str__(self):
        return f"{self.lead_code} - {self.lead_name}"