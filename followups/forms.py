from django import forms
from django.utils import timezone

from crm_core.authorization import assignable_users, visible_customers, visible_leads
from followups.models import FollowUp


class FollowUpForm(forms.ModelForm):
    class Meta:
        model = FollowUp
        fields = ("customer", "lead", "follow_up_date", "follow_up_type", "subject", "remarks", "assigned_to")
        widgets = {"follow_up_date": forms.DateInput(attrs={"type": "date"}), "remarks": forms.Textarea(attrs={"rows": 4})}

    def __init__(self, *args, user, **kwargs):
        super().__init__(*args, **kwargs)
        self.original_follow_up_date = self.instance.follow_up_date if self.instance.pk else None
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
        if not self.instance.pk and cleaned.get("follow_up_date") and cleaned["follow_up_date"] < timezone.localdate():
            self.add_error("follow_up_date", "A new planned follow-up cannot be scheduled in the past.")
        if self.instance.pk and cleaned.get("follow_up_date") and cleaned["follow_up_date"] < timezone.localdate() and cleaned["follow_up_date"] != self.instance.follow_up_date:
            self.add_error("follow_up_date", "Reschedule a planned follow-up to today or a future date.")
        assignee = cleaned.get("assigned_to")
        if assignee and not assignable_users(self.user).filter(pk=assignee.pk).exists():
            self.add_error("assigned_to", "You cannot assign follow-ups to that user.")
        return cleaned


class FollowUpFilterForm(forms.Form):
    period = forms.ChoiceField(required=False, choices=(("", "Any date"), ("today", "Today"), ("week", "This week"), ("month", "This month"), ("custom", "Custom range")))
    date_from = forms.DateField(required=False, widget=forms.DateInput(attrs={"type": "date"}))
    date_to = forms.DateField(required=False, widget=forms.DateInput(attrs={"type": "date"}))
    status = forms.ChoiceField(required=False, choices=(("", "All statuses"), *FollowUp.Status.choices))
    follow_up_type = forms.ChoiceField(required=False, choices=(("", "All types"), *FollowUp.Type.choices))
    assigned_to = forms.ModelChoiceField(queryset=None, required=False, empty_label="All users")
    customer = forms.ModelChoiceField(queryset=None, required=False, empty_label="All customers")
    lead = forms.ModelChoiceField(queryset=None, required=False, empty_label="All leads")

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