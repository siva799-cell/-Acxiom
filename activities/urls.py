from django.urls import path

from activities import views


app_name = "activities"

urlpatterns = [
    path("", views.activity_list, name="list"),
    path("create/", views.activity_create, name="create"),
    path("<int:pk>/", views.activity_detail, name="detail"),
    path("<int:pk>/edit/", views.activity_edit, name="edit"),
    path("<int:pk>/status/<str:status>/", views.activity_status, name="status"),
]