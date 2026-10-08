from leads.models import Lead


ALLOWED_TRANSITIONS = {
    Lead.Status.NEW: {Lead.Status.CONTACTED, Lead.Status.UNQUALIFIED, Lead.Status.LOST},
    Lead.Status.CONTACTED: {Lead.Status.QUALIFIED, Lead.Status.UNQUALIFIED, Lead.Status.LOST},
    Lead.Status.QUALIFIED: {Lead.Status.LOST},
    Lead.Status.UNQUALIFIED: set(),
    Lead.Status.CONVERTED: set(),
    Lead.Status.LOST: set(),
}