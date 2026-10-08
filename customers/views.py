from django.contrib import messages
from django.core.paginator import Paginator
from django.db.models import Q
from django.shortcuts import get_object_or_404, redirect, render
from django.views.decorators.http import require_POST

from crm_core.authorization import visible_customers
from crm_core.authorization import visible_activities, visible_followups
from crm_core.decorators import crm_role_required
from customers.forms import CustomerForm
from customers.models import Customer
from customers.services import save_customer, set_customer_status


@crm_role_required
def customer_list(request):
    customers = visible_customers(request.user).select_related("created_by")
    query = request.GET.get("q", "").strip()
    status = request.GET.get("status", "all")
    if query:
        customers = customers.filter(
            Q(customer_code__icontains=query)
            | Q(customer_name__icontains=query)
            | Q(email__icontains=query)
            | Q(phone__icontains=query)
            | Q(company_name__icontains=query)
        )
    if status in Customer.Status.values:
        customers = customers.filter(status=status)
    elif status != "all":
        status = "all"
    page_obj = Paginator(customers, 15).get_page(request.GET.get("page"))
    return render(request, "customers/list.html", {"page_obj": page_obj, "q": query, "status_filter": status})


@crm_role_required
def customer_create(request):
    if request.method == "POST":
        form = CustomerForm(request.POST)
        if form.is_valid():
            customer = save_customer(form=form, actor=request.user)
            messages.success(request, "Customer created.")
            return redirect("customers:detail", pk=customer.pk)
    else:
        form = CustomerForm()
    return render(request, "customers/form.html", {"form": form, "is_create": True})


@crm_role_required
def customer_detail(request, pk):
    customer = get_object_or_404(visible_customers(request.user).select_related("created_by"), pk=pk)
    return render(request, "customers/detail.html", {
        "customer": customer,
        "related_followups": visible_followups(request.user).filter(customer=customer).select_related("assigned_to")[:5],
        "related_activities": visible_activities(request.user).filter(customer=customer).select_related("assigned_to")[:5],
    })


@crm_role_required
def customer_edit(request, pk):
    customer = get_object_or_404(visible_customers(request.user), pk=pk)
    if request.method == "POST":
        form = CustomerForm(request.POST, instance=customer)
        if form.is_valid():
            save_customer(form=form, actor=request.user, instance=customer)
            messages.success(request, "Customer updated.")
            return redirect("customers:detail", pk=customer.pk)
    else:
        form = CustomerForm(instance=customer)
    return render(request, "customers/form.html", {"form": form, "customer": customer})


@crm_role_required
@require_POST
def customer_toggle_status(request, pk):
    customer = get_object_or_404(visible_customers(request.user), pk=pk)
    status = Customer.Status.INACTIVE if customer.status == Customer.Status.ACTIVE else Customer.Status.ACTIVE
    set_customer_status(customer=customer, actor=request.user, status=status)
    messages.success(request, f"Customer {status.lower()}.")
    return redirect("customers:detail", pk=customer.pk)