from django.urls import path

from followups import views


app_name = "followups"

urlpatterns = [
    path("", views.followup_list, name="list"),
    path("create/", views.followup_create, name="create"),
    path("<int:pk>/", views.followup_detail, name="detail"),
    path("<int:pk>/edit/", views.followup_edit, name="edit"),
    path("<int:pk>/status/<str:status>/", views.followup_status, name="status"),
]