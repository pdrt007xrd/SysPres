# Manual de Usuario - SysPres (Versión 1)

Este manual describe el uso de la aplicación en su estado actual.

---

## 1. Objetivo de la aplicación
SysPres permite gestionar:
- Clientes
- Préstamos
- Pagos
- Reportes
- Configuración (solo Admin)

Incluye control de acceso por roles/permisos, generación de PDF y registro de actividad.

---

## 2. Acceso al sistema
1. Abrir la URL de la aplicación.
2. Ingresar `Usuario` y `Contraseña`.
3. Presionar `Ingresar`.

Si el usuario está inactivo, el sistema bloquea el acceso.

---

## 3. Roles y permisos

### 3.1 Admin
- Acceso total.
- Puede entrar a `Configuración`.
- Puede crear usuarios y asignar permisos.
- Puede editar datos de empresa.

### 3.2 Empleados/Cobradores/Usuarios
- Acceso según permisos asignados:
  - Dashboard
  - Clientes
  - Préstamos
  - Pagos
  - Reportes

---

## 4. Dashboard
Muestra indicadores principales:
- Préstamos activos
- Préstamos en atraso
- Total atrasado
- Capital prestado
- Monto global a cobrar
- Interés recolectado
- Actividad reciente

---

## 5. Módulo Clientes

### 5.1 Registrar cliente
1. Ir a `Clientes`.
2. Clic en `Nuevo cliente`.
3. Completar datos personales, laborales y garante (si aplica).
4. Guardar.

### 5.2 Editar cliente
1. Ir a `Clientes`.
2. Clic en `Editar`.
3. Actualizar datos y guardar.

### 5.3 Ver detalle
- Clic en `Ver` para consultar información completa y préstamos del cliente.

### 5.4 Buscar cliente
- Usar el campo `Buscar por nombre`.

---

## 6. Módulo Préstamos

### 6.1 Crear préstamo
1. Ir a `Préstamos`.
2. Clic en `Nuevo préstamo`.
3. Seleccionar cliente.
4. Completar monto, tasa, cantidad de pagos, frecuencia y fecha.
5. Confirmar creación.

El sistema calcula automáticamente:
- Interés
- Total a pagar
- Valor por cuota

### 6.2 Editar préstamo
- Permitido cuando no hay cuotas pagadas.

### 6.3 Marcar cuota pagada
- Desde detalle del préstamo, botón `Marcar pagada`.

### 6.4 Reestructurar por pago solo interés
En detalle del préstamo existe:
- `Reestructurar (pago solo interés)`

Uso:
- Cuando cliente paga solo interés del ciclo.
- El sistema registra ese pago.
- Reinicia el préstamo al próximo ciclo.
- Genera nuevo cronograma.

---

## 7. Módulo Pagos

### 7.1 Flujo general
1. Ir a `Pagos`.
2. Escribir nombre del cliente.
3. El sistema carga automáticamente el último préstamo activo.
4. Seleccionar tipo de pago.

### 7.2 Tipos de pago
- `Normal`: aplica monto contra cuotas pendientes.
- `SoloInteres`: aplica interés pendiente del ciclo y reestructura.

### 7.3 Pago normal
- Ingresar `Monto a aplicar`.
- El sistema distribuye automáticamente a cuotas en orden.

### 7.4 Pago solo interés
- Monto se autocompleta con interés pendiente del ciclo.
- Se bloquea edición manual del monto.

### 7.5 Método de pago
- Efectivo
- Transferencia
- Tarjeta

Si es `Efectivo`:
- Ingresar monto recibido.
- El sistema calcula devuelta.

### 7.6 Recibo PDF
Después de registrar pago se abre recibo PDF (A4 o 80mm).

El recibo incluye:
- Cliente
- Usuario cobrador
- Tipo de pago
- Método
- Detalle aplicado
- Balance pendiente
- Capital abonado
- Interés abonado
- Total pagado
- Efectivo recibido
- Devuelta

---

## 8. Módulo Reportes
En el sidebar entrar a `Reportes`.

### 8.1 Reportes disponibles
- Clientes PDF
- Préstamos PDF
- Histórico de pagos PDF
- Total por cliente PDF

### 8.2 Reporte total por cliente
Muestra por cliente:
- Total prestado
- Interés generado
- Interés cobrado
- Pagos solo interés
- Total

También muestra totales globales al final.

---

## 9. Módulo Configuración (Admin)

### 9.1 Datos de empresa
- Editar nombre, dirección, teléfono y ciudad.

### 9.2 Crear empleados/cobradores/usuarios
- Definir usuario, nombre, contraseña, confirmación, rol, estado y permisos iniciales.

### 9.3 Permisos por usuario
- Ajustar permisos por módulo.
- Activar/inactivar usuarios.

---

## 10. Notificaciones
La aplicación muestra notificaciones toast para:
- Éxitos
- Advertencias
- Errores

Se usan en los flujos de CRUD y configuración.

---

## 11. Seguridad funcional implementada
- Autenticación por cookie.
- Fallback de autorización: requiere login.
- Policies por permisos.
- Solo Admin accede a Configuración.
- Usuarios inactivos no pueden iniciar sesión.
- Cabeceras de seguridad HTTP.

---

## 12. Buenas prácticas de operación
- Registrar clientes completos antes de crear préstamos.
- Verificar monto recibido en efectivo para evitar diferencias.
- Usar `SoloInteres` únicamente cuando aplique reestructuración real.
- Revisar `Reportes` semanalmente para control financiero.

---

## 13. Versión del manual
- Documento: `MANUAL_APP_V1.md`
- Versión: `1.0`
- Estado: Primera versión operativa.

---
Desarrollado por Pedro Peguero, 829-966-1111 (WhatsApp).
