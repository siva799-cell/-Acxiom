import re

from django.conf import settings
from django.core.validators import RegexValidator
from django.db import models


phone_validator = RegexValidator(r"^\+?[0-9]{7,15}$", "Enter a phone number containing 7 to 15 digits.")


class Customer(models.Model):
    class Status(models.TextChoices):
        ACTIVE = "Active", "Active"
        INACTIVE = "Inactive", "Inactive"

    customer_code = models.CharField(max_length=16, unique=True, blank=True)
    customer_name = models.CharField(max_length=160)
    email = models.EmailField(blank=True)
    phone = models.CharField(max_length=16, blank=True, validators=[phone_validator])
    company_name = models.CharField(max_length=160, blank=True)
    address = models.TextField(blank=True)
    city = models.CharField(max_length=100, blank=True)
    state = models.CharField(max_length=100, blank=True)
    status = models.CharField(max_length=10, choices=Status.choices, default=Status.ACTIVE, db_index=True)
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT, related_name="created_customers")
    modified_at = models.DateTimeField(auto_now=True)

    class Meta:
        ordering = ("-created_at", "-pk")
        indexes = [models.Index(fields=("customer_name", "company_name"))]

    def save(self, *args, **kwargs):
        if not self.customer_code:
            super().save(*args, **kwargs)
            self.customer_code = f"CUS-{self.pk:06d}"
            type(self).objects.filter(pk=self.pk).update(customer_code=self.customer_code)
            return
        super().save(*args, **kwargs)

    @staticmethod
    def normalize_phone(value):
        digits = re.sub(r"\D", "", value or "")
        return ("+" if (value or "").strip().startswith("+") else "") + digits

    def __str__(self):
        return f"{self.customer_code} - {self.customer_name}"