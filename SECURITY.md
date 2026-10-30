# Política de seguridad

## Cómo reportar una vulnerabilidad

**No abras un issue público ni publiques detalles explotables.**

Usa la pestaña **Security** del repositorio en GitHub → **Report a vulnerability**
(GitHub Security Advisories). El reporte queda privado y visible solo para los
mantenedores.

Incluye, si es posible:

- Versión o commit afectado.
- Pasos para reproducir el problema.
- Impacto esperado (confidencialidad, integridad, disponibilidad).
- Cualquier propuesta de mitigación.

## Qué se considera una vulnerabilidad

- Ejecución de código o escalada de privilegios.
- Autenticación o autorización evadible (JWT, cookies, RBAC por permisos).
- Inyección SQL, XSS, CSRF, SSRF o path traversal.
- Exposición de secretos, datos personales o documentos.
- Dependencias con vulnerabilidades conocidas explotables.

## Fuera de alcance

- Falta de cabeceras de seguridad en el entorno de desarrollo.
- Configuraciones locales de ejemplo (`appsettings.Development.json`, `.env.example`).
- Ingeniería social o ataques físicos.

## Tiempos de respuesta

Este es un proyecto académico/institucional sin SLA formal. Se intentará acusar
recibo y coordinar la divulgación responsable según la disponibilidad de los
mantenedores.
