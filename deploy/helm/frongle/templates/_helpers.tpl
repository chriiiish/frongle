{{- define "frongle.dbHost" -}}
{{ required "database.host is required" .Values.database.host -}}
{{- end }}

{{- define "frongle.dbSecret" -}}
{{ required "database.existingSecret is required" .Values.database.existingSecret -}}
{{- end }}

{{- define "frongle.keycloakName" -}}{{ .Release.Name }}-keycloak{{- end }}
