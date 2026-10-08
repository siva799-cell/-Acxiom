from django import forms

from activities.models import Activity
from crm_core.authorization import assignable_users, visible_customers, visible_leads


class ActivityForm(forms.ModelForm):
    class Meta:
        model = Activity
        fields = ("activity_type", "subject", "description", "activity_date", "customer", "lead", "assigned_to")
        widgets = {"activity_date": forms.DateInput(attrs={"type": "date"}), "description": forms.Textarea(attrs={"rows": 4})}

    def __init__(self, *args, user, **kwargs):
        super().__init__(*args, **kwargs)
        self.original_assigned_to_id = self.instance.assigned_to_id if self.instance.pk else None
        self.user = user
        self.fields["customer"].queryset = visible_customers(user).order_by("customer_name")
        self.fields["lead"].queryset = visible_leads(user).order_by("lead_name")
        self.fields["assigned_to"].queryset = assignable_users(user)
        for field in self.fields.values():
            field.widget.attrs.setdefault("class", "form-select" if isinstance(field.widget, forms.Select) else "form-control")

    def clean(self):
        cleaned = super().clean()
        if bool(cleaned.get("customer")) == bool(cleaned.get("lead")):
            raise forms.ValidationError("Select exactly one customer or lead.")
        assignee = cleaned.get("assigned_to")
        if assignee and not assignable_users(self.user).filter(pk=assignee.pk).exists():
            self.add_error("assigned_to", "You cannot assign activities to that user.")
        return cleaned


class ActivityFilterForm(forms.Form):
    activity_type = forms.ChoiceField(required=False, choices=(("", "All types"), *Activity.Type.choices))
    status = forms.ChoiceField(required=False, choices=(("", "All statuses"), *Activity.Status.choices))
    assigned_to = forms.ModelChoiceField(queryset=None, required=False, empty_label="All users")
    customer = forms.ModelChoiceField(queryset=None, required=False, empty_label="All customers")
    lead = forms.ModelChoiceField(queryset=None, required=False, empty_label="All leads")
    date_from = forms.DateField(required=False, widget=forms.DateInput(attrs={"type": "date"}))
    date_to = forms.DateField(required=False, widget=forms.DateInput(attrs={"type": "date"}))

    def __init__(self, *args, user, **kwargs):
        super().__init__(*args, **kwargs)
        self.fields["assigned_to"].queryset = assignable_users(user)
        self.fields["customer"].queryset = visible_customers(user).order_by("customer_name")
        self.fields["lead"].queryset = visible_leads(user).order_by("lead_name")
        for field in self.fields.values():
            field.widget.attrs.setdefault("class", "form-select" if isinstance(field.widget, forms.Select) else "form-control")

    def clean(self):
        cleaned = super().clean()
        if cleaned.get("date_from") and cleaned.get("date_to") and cleaned["date_from"] > cleaned["date_to"]:
            raise forms.ValidationError("The start date must be on or before the end date.")
        return cleaned