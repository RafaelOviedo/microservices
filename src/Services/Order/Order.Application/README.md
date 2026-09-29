# Order.Application

Creación, consulta por ID, listado histórico paginado, confirmación y cancelación de órdenes. Define los contratos del
repositorio, del almacenamiento de procesamiento y de los clientes HTTP; validaciones
FluentValidation, DTOs y mapping AutoMapper.

La coordinación guarda la intención antes de invocar Product, utiliza el ID de la orden como
identificador estable de la operación de stock y persiste los reintentos. Permite recuperar
confirmaciones y compensaciones interrumpidas sin duplicar descuentos ni devoluciones.
La creación deja la orden pendiente: la confirmación se solicita explícitamente.

El historial admite filtros por cliente, estado y fechas de creación. Sus validaciones limitan
el tamaño de página a 100 y rechazan rangos inválidos. Se consultan únicamente los datos
guardados en Order, incluyendo las cantidades finales de las compras confirmadas.
