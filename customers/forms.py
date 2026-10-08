import re

from django import forms
from django.db.models import Q

from customers.models import Customer


class CustomerForm(forms.ModelForm):
    phone = forms.CharField(required=False, max_length=32)

    class Meta:
        model = Customer
        fields = ("customer_name", "email", "phone", "company_name", "address", "city", "state", "status")
        widgets = {"address": forms.Textarea(attrs={"rows": 3})}

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        for field in self.fields.values():
            field.widget.attrs.setdefault("class", "form-select" if isinstance(field.widget, forms.Select) else "form-control")

    def clean_customer_name(self):
        name = " ".join(self.cleaned_data["customer_name"].split())
        if not name:
            raise forms.ValidationError("Customer name is required.")
        return name

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
        queryset = Customer.objects.all()
        if self.instance.pk:
            queryset = queryset.exclude(pk=self.instance.pk)
        email = cleaned.get("email")
        phone = cleaned.get("phone")
        name = cleaned.get("customer_name")
        company = cleaned.get("company_name", "").strip()
        duplicates = Q()
        has_duplicate_key = False
        if email:
            duplicates |= Q(email__iexact=email)
            has_duplicate_key = True
        if phone:
            duplicates |= Q(phone=phone)
            has_duplicate_key = True
        if name and company:
            duplicates |= Q(customer_name__iexact=name, company_name__iexact=company)
            has_duplicate_key = True
        if has_duplicate_key and queryset.filter(duplicates).exists():
            raise forms.ValidationError("A customer with the same email, phone, or name and company already exists.")
        return cleaned