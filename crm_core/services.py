from crm_core.models import AuditEvent


def record_event(*, actor, event_type, instance):
    """Persist a minimal audit event without copying user-entered or secret data."""
    return AuditEvent.objects.create(
        actor=actor if getattr(actor, "is_authenticated", False) else None,
        event_type=event_type,
        object_type=instance._meta.label_lower[:40],
        object_id=str(instance.pk),
    )