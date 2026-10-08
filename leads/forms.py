import re

from django import forms
from django.db.models import Q

from crm_core.authorization import assignable_users
from leads.models import Lead
from leads.rules import ALLOWED_TRANSITIONS


class LeadForm(forms.ModelForm):
    phone = forms.CharField(required=False, max_length=32)

    class Meta:
        model = Lead
        fields = ("lead_name", "email", "phone", "company_name", "source", "status", "priority", "expected_value", "notes", "assigned_to")
        widgets = {"notes": forms.Textarea(attrs={"rows": 4}), "expected_value": forms.NumberInput(attrs={"min": "0", "step": "0.01"})}

    def __init__(self, *args, user, **kwargs):
        super().__init__(*args, **kwargs)
        self.original_status = self.instance.status if self.instance.pk else None
        self.original_assigned_to_id = self.instance.assigned_to_id if self.instance.pk else None
        self.user = user
        self.fields["assigned_to"].queryset = assignable_users(user)
        self.fields["status"].choices = [choice for choice in Lead.Status.choices if choice[0] != Lead.Status.CONVERTED]
        for field in self.fields.values():
            field.widget.attrs.setdefault("class", "form-select" if isinstance(field.widget, forms.Select) else "form-control")
        if not self.instance.pk:
            self.fields["status"].initial = Lead.Status.NEW

    def clean_lead_name(self):
        value = " ".join(self.cleaned_data["lead_name"].split())
        if not value:
            raise forms.ValidationError("Lead name is required.")
        return value

    def clean_email(self):
        return (self.cleaned_data.get("email") or "").strip().lower()

    def clean_phone(self):
        value = self.cleaned_data.get("phone") or ""
        digits = re.sub(r"\D", "", value)
        if value and not 7 <= len(digits) <= 15:
            raise forms.ValidationError("Enter a phone number containing 7 to 15 digits.")
        return ("+" if value.strip().startswith("+") else "") + digits

    def clean(self):
        cleaned = super().clean()
        assigned = cleaned.get("assigned_to")
        if assigned and not assignable_users(self.user).filter(pk=assigned.pk).exists():
            self.add_error("assigned_to", "You cannot assign leads to that user.")
        if cleaned.get("status") == Lead.Status.CONVERTED:
            self.add_error("status", "Use the lead conversion action to convert a lead.")
        if self.instance.pk and cleaned.get("status") != self.instance.status:
            allowed = ALLOWED_TRANSITIONS.get(self.instance.status, set())
            if cleaned.get("status") not in allowed:
                self.add_error("status", "That lead status transition is not allowed.")
        queryset = Lead.objects.all()
        if self.instance.pk:
            queryset = queryset.exclude(pk=self.instance.pk)
        duplicate = Q()
        keys = False
        if cleaned.get("email"):
            duplicate |= Q(email__iexact=cleaned["email"])
            keys = True
        if cleaned.get("phone"):
            duplicate |= Q(phone=cleaned["phone"])
            keys = True
        name, company = cleaned.get("lead_name"), cleaned.get("company_name", "").strip()
        if name and company:
            duplicate |= Q(lead_name__iexact=name, company_name__iexact=company)
            keys = True
        if keys and queryset.filter(duplicate).exists():
            raise forms.ValidationError("A lead with the same email, phone, or name and company already exists.")
        return cleaned