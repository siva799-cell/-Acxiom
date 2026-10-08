from django.urls import path

from accounts.views import CrmLoginView, logout_view


app_name = "accounts"

urlpatterns = [
    path("login/", CrmLoginView.as_view(), name="login"),
    path("logout/", logout_view, name="logout"),
]