from django.urls import path

from leads import views


app_name = "leads"

urlpatterns = [
    path("", views.lead_list, name="list"),
    path("create/", views.lead_create, name="create"),
    path("<int:pk>/", views.lead_detail, name="detail"),
    path("<int:pk>/edit/", views.lead_edit, name="edit"),
    path("<int:pk>/convert/", views.lead_convert, name="convert"),
    path("<int:pk>/status/", views.lead_status, name="status"),
]