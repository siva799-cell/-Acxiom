from django.db import migrations


ROLE_NAMES = ("Admin", "Manager", "SalesExecutive")


def create_role_groups(apps, schema_editor):
    group_model = apps.get_model("auth", "Group")
    for role_name in ROLE_NAMES:
        group_model.objects.using(schema_editor.connection.alias).get_or_create(name=role_name)


class Migration(migrations.Migration):
    initial = True

    dependencies = [
        ("auth", "0012_alter_user_first_name_max_length"),
    ]

    operations = [
        migrations.RunPython(create_role_groups, migrations.RunPython.noop),
    ]