# Product.Domain

Aquí se incorporarán el agregado Product, el value object Money y las reglas de negocio en el punto 2.
Esta capa no depende de Entity Framework, HTTP ni otros proyectos de la solución.

Todo borrado será lógico: IsDeleted y DeletedAtUtc. Un producto eliminado no estará disponible
para consultas habituales ni nuevas compras, y se conservará en la base de datos.
