from django.urls import path

from customers import views


app_name = "customers"

urlpatterns = [
    path("", views.customer_list, name="list"),
    path("create/", views.customer_create, name="create"),
    path("<int:pk>/", views.customer_detail, name="detail"),
    path("<int:pk>/edit/", views.customer_edit, name="edit"),
    path("<int:pk>/status/", views.customer_toggle_status, name="toggle_status"),
]