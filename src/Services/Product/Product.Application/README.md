# Product.Application

`ProductService` coordina los casos de uso mediante `IProductRepository`.
FluentValidation valida las solicitudes antes de ejecutar cambios. El dominio también protege sus invariantes.
AutoMapper convierte entidades a respuestas HTTP; las entradas se aplican mediante los métodos del agregado.
Los DTOs de entrada no permiten asignar identificadores, fechas ni indicadores de borrado.

La baja utiliza el método `Delete` del agregado. Las consultas del repositorio excluyen productos eliminados.
