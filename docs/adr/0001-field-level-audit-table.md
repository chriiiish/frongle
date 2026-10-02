# Record changes in a field-level audit table

Frongle must show who changed what and when, so each change to an Asset, Event, or Area writes one Audit Record per field, with the old value, the new value, the user, and the time. We rejected "created by" and "updated by" columns because they keep only the last change. The table is append-only and has the same tenant row-level security as other tables.
