# Product.Domain

El agregado `Product` controla la creación, actualización y baja lógica del producto.
`Money` es un value object inmutable que admite precios positivos con hasta dos decimales.
Esta capa no depende de EF Core, HTTP ni los demás proyectos.

`Delete` establece `IsDeleted` y `DeletedAtUtc` en UTC y conserva los datos.
Un producto eliminado no admite cambios; repetir la baja en el dominio conserva la fecha original.
`Version` permite detectar escrituras concurrentes mediante la configuración de persistencia.
