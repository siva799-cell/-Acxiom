import hashlib

from django.contrib.auth import get_user_model
from django.contrib.auth.backends import ModelBackend
from django.core.cache import cache


FAILURE_LIMIT = 5
LOCKOUT_SECONDS = 15 * 60


class LockoutModelBackend(ModelBackend):
    """Django's password authentication with a per-account temporary lockout."""

    def authenticate(self, request, username=None, password=None, **kwargs):
        user_model = get_user_model()
        username = username or kwargs.get(user_model.USERNAME_FIELD)
        if username is None or password is None:
            return None

        normalized_username = user_model.normalize_username(username)
        account_key = hashlib.sha256(normalized_username.casefold().encode()).hexdigest()
        lock_key = f"auth-lock:{account_key}"
        failures_key = f"auth-failures:{account_key}"
        if cache.get(lock_key):
            return None

        try:
            user = user_model._default_manager.get_by_natural_key(username)
        except user_model.DoesNotExist:
            user_model().set_password(password)
            user = None

        if user is not None and user.check_password(password) and self.user_can_authenticate(user):
            cache.delete(failures_key)
            return user

        cache.add(failures_key, 0, timeout=LOCKOUT_SECONDS)
        failures = cache.incr(failures_key)
        if failures >= FAILURE_LIMIT:
            cache.set(lock_key, True, timeout=LOCKOUT_SECONDS)
        return None