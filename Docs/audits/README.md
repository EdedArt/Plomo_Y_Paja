# Auditorías

Aquí el agente deja un archivo por fase: `PHASE-<X>-audit.md`, escrito en español a partir de
`docs/phases/AUDIT_TEMPLATE.md`.

Cada auditoría empieza con `ESTADO: PENDIENTE DE REVISIÓN`. Solo cuando el orquestador la aprueba,
David cambia esa línea por `ESTADO: LUZ VERDE — aprobado por el orquestador` y hace commit.
La fase siguiente no arranca sin esa línea.
