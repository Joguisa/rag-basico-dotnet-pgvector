# Documentación técnica - API de Pedidos de Acme Software

Esta es la documentación de referencia de la API interna de Pedidos (versión 2.3), consumida por
el equipo de e-commerce para crear, consultar y cancelar pedidos de clientes.

## Autenticación

Todos los endpoints requieren un header `Authorization: Bearer <token>`. El token se obtiene del
servicio de autenticación interno y tiene una validez de 60 minutos. Pasado ese tiempo, cualquier
request devuelve `401 Unauthorized` con el cuerpo `{"error": "token_expired"}`.

## POST /pedidos

Crea un nuevo pedido. Recibe un JSON con los campos `clienteId` (string, obligatorio),
`items` (array de objetos con `productoId` y `cantidad`, obligatorio, al menos 1 elemento), y
`direccionEnvioId` (string, obligatorio).

Responde `201 Created` con el pedido creado, incluyendo un `pedidoId` autogenerado y el campo
`estado` en `"pendiente"`. Si `clienteId` no existe, responde `404 Not Found`. Si algún
`productoId` no tiene stock suficiente, responde `409 Conflict` con el detalle de qué producto
falló.

## GET /pedidos/{pedidoId}

Devuelve el detalle completo de un pedido: items, estado actual, dirección de envío, y el
historial de cambios de estado con timestamp de cada transición. Los estados posibles son:
`pendiente`, `confirmado`, `en_preparacion`, `enviado`, `entregado` y `cancelado`.

## DELETE /pedidos/{pedidoId}

Cancela un pedido. Solo se permite cancelar pedidos en estado `pendiente` o `confirmado`. Si el
pedido ya está `en_preparacion` o en un estado posterior, responde `422 Unprocessable Entity` con
`{"error": "no_cancelable_en_este_estado"}`. Un pedido cancelado exitosamente pasa a estado
`cancelado` y no puede volver a modificarse.

## Códigos de error comunes

| Código | Significado | Causa típica |
|---|---|---|
| 400 | Bad Request | Falta un campo obligatorio o tiene un tipo de dato incorrecto. |
| 401 | Unauthorized | Token ausente, inválido o expirado. |
| 404 | Not Found | El `pedidoId` o `clienteId` no existe. |
| 409 | Conflict | Stock insuficiente al crear el pedido. |
| 422 | Unprocessable Entity | La operación no es válida para el estado actual del pedido. |
| 429 | Too Many Requests | Se superó el límite de 100 requests por minuto por token. |
| 500 | Internal Server Error | Error no controlado del lado del servidor. Reintentar con backoff. |

## Límites de uso (rate limiting)

Cada token tiene un límite de 100 requests por minuto. Al superarlo, la API responde
`429 Too Many Requests` con un header `Retry-After` indicando los segundos a esperar antes del
siguiente intento. Los reintentos automáticos deben implementar backoff exponencial, comenzando
en 1 segundo y duplicando hasta un máximo de 30 segundos entre reintentos.
