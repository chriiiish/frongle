{{- define "frongle.dbHost" -}}
{{- if .Values.postgres.enabled }}{{ .Release.Name }}-postgres{{ else }}{{ required "database.host is required when postgres.enabled is false" .Values.database.host }}{{ end -}}
{{- end }}

{{- define "frongle.dbSecret" -}}
{{- if .Values.postgres.enabled }}{{ .Release.Name }}-db{{ else }}{{ required "database.existingSecret is required when postgres.enabled is false" .Values.database.existingSecret }}{{ end -}}
{{- end }}

{{- define "frongle.dbAppSecret" -}}
{{- if .Values.postgres.enabled }}{{ .Release.Name }}-db-app{{ else }}{{ required "database.appExistingSecret is required when postgres.enabled is false" .Values.database.appExistingSecret }}{{ end -}}
{{- end }}

{{- define "frongle.keycloakName" -}}{{ .Release.Name }}-keycloak{{- end }}
