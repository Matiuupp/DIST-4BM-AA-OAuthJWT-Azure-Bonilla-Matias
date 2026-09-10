# Arquitectura distribuida segura con OAuthJWT y despliegue en Azure

Sistema de microservicios para la gestión de pacientes e historiales clínicos, desarrollado con .NET 10, Entity Framework Core, SQL Server, RabbitMQ, Docker y un API Gateway con YARP, protegido mediante autenticación JWT y desplegado en Microsoft Azure.

**Materia:** Aplicaciones Distribuidas  
**Curso:** Cuarto "B" Matutino - Desarrollo de Software  
**Estudiante:** Matías Bonilla  
**Tema asignado:** Pacientes e Historial Clínico

---

## Servicios desplegados en Azure

> **Antes de hacer clic:** la raíz de cada servicio devuelve un error 404. Es el comportamiento esperado — los endpoints viven bajo `/api/...` y la documentación bajo `/swagger`. Use los enlaces de la tabla, que apuntan directamente a Swagger.

| Servicio | Enlace |
|---|---|
| **API Gateway** (entrada principal) | https://apigateway.mangorock-874cd57f.centralus.azurecontainerapps.io |
| OAuthJWT — obtener el token | https://oauthjwt.mangorock-874cd57f.centralus.azurecontainerapps.io/swagger |
| Pacientes.Api | https://pacientes.mangorock-874cd57f.centralus.azurecontainerapps.io/swagger |
| HistorialClinico.Api | https://historial.mangorock-874cd57f.centralus.azurecontainerapps.io/swagger |

RabbitMQ está desplegado con **ingress interno**: solo los microservicios dentro del entorno pueden alcanzarlo. No tiene URL pública, por seguridad.

---

## Cómo probar el sistema

Los dos microservicios tienen el botón **Authorize** en Swagger, así que puede probarse todo desde el navegador sin necesidad de Postman.

### Paso 1 — Comprobar que la protección funciona

En el Swagger de Pacientes, ejecute `GET /api/Pacientes` sin autorizarse. La respuesta es **401 Unauthorized**.

### Paso 2 — Obtener un token

En el Swagger de OAuthJWT, ejecute `POST /api/Auth/login` con:

```json
{
  "usuario": "admin",
  "password": "1234"
}
```

La respuesta contiene el token, el usuario y su rol:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "usuario": "admin",
  "rol": "Administrador"
}
```

### Paso 3 — Autorizarse y repetir

Copie el valor de `token` (solo el token, sin comillas). Vuelva al Swagger de Pacientes, pulse **Authorize** arriba a la derecha, pegue el token y confirme.

Ahora `GET /api/Pacientes` responde **200 OK** con la lista.

El mismo token sirve para HistorialClinico.Api, porque los tres servicios comparten la misma clave de firma.

### Usuarios disponibles

| Usuario | Contraseña | Rol | Permisos |
|---|---|---|---|
| admin | 1234 | Administrador | Consultar, crear, editar y eliminar |
| medico | 1234 | Usuario | Solo consultar |

Con el usuario `medico`, un `POST` responde **403 Forbidden**: el token es válido pero el rol no alcanza. Es la diferencia entre autenticación (quién eres) y autorización (qué puedes hacer).

### Probar la mensajería asíncrona

1. Con un token de `admin`, cree un paciente con `POST /api/Pacientes`
2. Consulte `GET /api/HistorialClinico`

Aparecerá un historial nuevo con el número `HC-XXXX` correspondiente al id del paciente, creado automáticamente por el consumidor de RabbitMQ.

---

## Arquitectura
          Cliente / Postman
                 |
                 v
          +--------------+
          | API Gateway  |
          +--------------+
                 |
   +-------------+-------------+
   |             |             |
   v             v             v

+--------+ +-----------+ +-----------+
|OAuthJWT| | Pacientes | | Historial |
+--------+ +-----------+ +-----------+
| ^
+-- RabbitMQ ---+
| |
v v
+------------------------+
| Azure SQL Database |
| PacientesDB HistorialDB|
+------------------------+


| Componente | Responsabilidad |
|---|---|
| **API Gateway** | Punto único de entrada. Enruta las peticiones con YARP. No valida tokens: los reenvía intactos |
| **OAuthJWT** | Servicio independiente de autenticación. Emite el token JWT firmado. No tiene base de datos |
| **Pacientes.Api** | CRUD de pacientes. Valida el token y publica eventos en RabbitMQ |
| **HistorialClinico.Api** | CRUD de historiales. Valida el token y consume los eventos |
| **RabbitMQ** | Transporta los mensajes entre los dos microservicios |

### El servicio OAuthJWT

En las prácticas de clase el microservicio de negocio era también el emisor del token. Aquí esa responsabilidad se separó en un servicio propio, de modo que la autenticación deja de estar acoplada a un microservicio concreto.

**Los tres servicios comparten la misma `Key`, `Issuer` y `Audience`.** OAuthJWT firma con esa clave y los microservicios verifican la firma con la misma. Si alguno de los tres valores difiere aunque sea en una letra, el token se rechaza aunque sea legítimo.

| Parámetro | Valor | Para qué sirve |
|---|---|---|
| Key | (secreta) | Clave de firma. Impide falsificar tokens |
| Issuer | OAuthJWT | Quién emitió el token |
| Audience | MicroserviciosPacientes | Para quién está destinado |
| ExpireMinutes | 60 | Cuánto tiempo es válido |

Un JWT no es secreto, es **firmado**: cualquiera puede leer su contenido, pero nadie puede modificarlo sin la clave. Por eso los claims llevan el usuario y el rol, nunca la contraseña.

---

## Base de datos

### Dos bases separadas

Cada microservicio tiene la suya: `PacientesDB` e `HistorialDB`. Si compartieran base, la separación sería solo aparente.

### La relación 1:N sin llave foránea

Un paciente puede tener varios historiales. Pero como las tablas viven en bases distintas, **SQL Server no permite una llave foránea entre ellas**.

El campo `hist_paciente_id` guarda el id del paciente como un número normal, sin restricción. La relación existe en la lógica de la aplicación, no en el motor.

**La ventaja:** cada servicio puede caerse o actualizarse sin arrastrar al otro.  
**El precio:** la base ya no valida que el paciente exista. Esa comprobación la hace el código.

### Usuarios dedicados por base

Los microservicios **no se conectan con el administrador del servidor**. Cada uno tiene su propio usuario con permisos de lectura y escritura únicamente sobre su base:

| Base | Usuario | Roles |
|---|---|---|
| PacientesDB | `usuario_pacientes` | db_datareader, db_datawriter |
| HistorialDB | `usuario_historialclinico` | db_datareader, db_datawriter |

Es el principio de mínimo privilegio: si un servicio se viera comprometido, el acceso quedaría limitado a sus propios datos. Ninguno puede crear o alterar tablas, ni acceder a la base del otro.

En Azure son **usuarios contenidos**, que existen solo dentro de su base y no a nivel de servidor. En local se usa el método clásico (login de servidor más usuario de base), porque SQL Server local no habilita usuarios contenidos por defecto.

### Estructura de las tablas

**tbl_paciente** (`PacientesDB`)

| Columna | Tipo | Notas |
|---|---|---|
| pac_id | int IDENTITY | Clave primaria |
| pac_cedula | varchar(10) | UNIQUE. Es texto y no número porque puede empezar en cero |
| pac_nombre | varchar(100) | Obligatorio |
| pac_apellido | varchar(100) | Obligatorio |
| pac_direccion | varchar(200) | Opcional |
| pac_estado | bit | Borrado lógico. Por defecto 1 |

**tbl_historialclinico** (`HistorialDB`)

| Columna | Tipo | Notas |
|---|---|---|
| hist_id | int IDENTITY | Clave primaria |
| hist_paciente_id | int | Id del paciente. **Sin FOREIGN KEY** |
| hist_numero | varchar(20) | UNIQUE. Número de historia clínica |
| hist_diagnostico | varchar(500) | Obligatorio |
| hist_tratamiento | varchar(500) | Opcional |
| hist_fecha | datetime | Por defecto la fecha actual |
| hist_estado | bit | Borrado lógico. Por defecto 1 |

### Scripts incluidos

| Archivo | Uso |
|---|---|
| `BaseDatos/PacientesDB.sql` | Ejecución local |
| `BaseDatos/HistorialClinicoDB.sql` | Ejecución local |
| `BaseDatos/Azure_PacientesDB.sql` | Ejecución en Azure SQL |
| `BaseDatos/Azure_HistorialClinicoDB.sql` | Ejecución en Azure SQL |

**Por qué dos versiones.** Azure SQL tiene tres diferencias respecto a SQL Server local:

1. No admite `CREATE DATABASE` dentro de un script — las bases se crean desde el portal o con Azure CLI
2. No admite `USE base` — cada conexión está atada a una sola base, hay que seleccionarla antes de ejecutar
3. Los usuarios se crean como usuarios contenidos, sin login de servidor

---

## Comunicación asíncrona con RabbitMQ

| Cola | Publica | Consume | Efecto |
|---|---|---|---|
| `paciente_creado` | Pacientes.Api | HistorialClinico.Api | Crea un historial inicial `HC-XXXX` |
| `paciente_desactivado` | Pacientes.Api | HistorialClinico.Api | Desactiva todos los historiales del paciente |

### Por qué mensajería y no HTTP directo

Si `HistorialClinico.Api` estuviera caído, una llamada HTTP fallaría en el acto y el paciente quedaría sin historial. Con RabbitMQ el mensaje **espera en la cola** hasta que el servicio vuelva a levantarse.

Se verificó apagando el consumidor, creando un paciente y comprobando que el mensaje quedaba pendiente hasta que el servicio volvía.

### Confirmación manual de mensajes

Los consumidores usan `autoAck: false`. El mensaje no se borra al entregarse, sino cuando el código confirma con `BasicAck`, después de guardar en la base. Si algo falla, `BasicNack` con `requeue: true` lo devuelve a la cola.

Con `autoAck: true` el mensaje se borraría al entregarlo: si el guardado fallaba después, se perdía. Es la diferencia entre firmar el recibo de un paquete al recibirlo o después de comprobar que llegó bien.

### La cascada manual

Al desactivar un paciente con varios historiales, el consumidor los recorre y los desactiva uno por uno. Si existiera una llave foránea con borrado en cascada, la base lo haría sola. Como no la hay, ese `foreach` es la implementación manual de esa cascada.

---

## El borrado lógico

El `DELETE` no elimina registros. Cambia el estado a `false` y los listados filtran por activos.

Se decidió así porque en un sistema de salud la información clínica no puede perderse, y porque al borrar un paciente sus historiales quedarían huérfanos.

---

## Ejecución local con Docker Compose

### Requisitos

- Docker Desktop
- SQL Server con autenticación mixta y TCP/IP habilitado en el puerto 1433
- SQL Server Management Studio

### Paso 1 — Clonar

git clone https://github.com/Matiuupp/DIST-4BM-AA-OAuthJWT-Azure-Bonilla-Matias.git
cd DIST-4BM-AA-OAuthJWT-Azure-Bonilla-Matias


### Paso 2 — Crear las bases de datos

Abrir SSMS con **autenticación de Windows** y ejecutar **en este orden**:

1. `BaseDatos/PacientesDB.sql`
2. `BaseDatos/HistorialClinicoDB.sql`

El orden importa: el login se crea en el primer script y el segundo lo referencia.

### Paso 3 — Crear los usuarios de cada base

Con `PacientesDB` seleccionada:

```sql
CREATE LOGIN usuario_pacientes WITH PASSWORD = '<CONTRASENA>', CHECK_POLICY = OFF;
GO
CREATE USER usuario_pacientes FOR LOGIN usuario_pacientes;
GO
ALTER ROLE db_datareader ADD MEMBER usuario_pacientes;
GO
ALTER ROLE db_datawriter ADD MEMBER usuario_pacientes;
GO
```

Y lo equivalente en `HistorialDB` para `usuario_historialclinico`.

### Paso 4 — Revisar el compose

En `docker-compose.yml`, verificar que las contraseñas de las cadenas de conexión coincidan con las que acaba de crear.

### Paso 5 — Levantar

docker compose up --build


La primera vez tarda varios minutos: descarga las imágenes de .NET 10 y compila los cuatro proyectos. Después basta con `docker compose up`.

El sistema está listo cuando aparece `Now listening on: http://[::]:8080` de los cuatro servicios.

### Paso 6 — Probar

| Qué | Dirección local |
|---|---|
| API Gateway | http://localhost:8000 |
| OAuthJWT | http://localhost:8089/swagger |
| Pacientes.Api | http://localhost:8087/swagger |
| HistorialClinico.Api | http://localhost:8088/swagger |
| Panel de RabbitMQ | http://localhost:15672 |

### Paso 7 — Detener

docker compose down


> No se puede ejecutar el proyecto desde Visual Studio y los contenedores al mismo tiempo: chocan por los puertos.

---

## Endpoints

Todas las peticiones pasan por el API Gateway.

### Autenticación

POST /api/Auth/login obtiene el token JWT


### Pacientes

GET /api/Pacientes lista los activos [token]
GET /api/Pacientes/{id} obtiene uno [token]
POST /api/Pacientes crea y publica el evento [Administrador]
PUT /api/Pacientes/{id} actualiza [Administrador]
DELETE /api/Pacientes/{id} desactiva y publica [Administrador]


### Historial clínico

GET /api/HistorialClinico lista los activos [token]
GET /api/HistorialClinico/{id} obtiene uno [token]
GET /api/HistorialClinico/paciente/{idPaciente} los de un paciente [token]
POST /api/HistorialClinico crea [Administrador]
PUT /api/HistorialClinico/{id} edita diagnóstico [Administrador]
DELETE /api/HistorialClinico/{id} desactiva [Administrador]


---

## Despliegue en Azure

| Recurso | Nombre | Región |
|---|---|---|
| Resource Group | rg-pacientes-aa | East US |
| Container Registry | acrpacientesmatias | East US |
| SQL Server | sql-pacientes-matias-26 | Central US |
| Container Apps Environment | env-pacientes | Central US |

El detalle completo está en `MEMORIA_COMANDOS_AZURE.txt`.

### Diferencias entre local y Azure

**Direcciones entre servicios.** En Docker Compose los servicios se llaman por su nombre de red (`http://pacientes:8080`). En Container Apps cada uno tiene su dominio público y el gateway los alcanza por ahí.

**Conexión a la base.** En local se usa `Encrypt=False`; Azure SQL exige `Encrypt=True` y el prefijo `tcp:` en el servidor.

**Secretos.** En Azure las cadenas de conexión, la clave JWT y la contraseña de RabbitMQ se guardan en el almacén de secretos del Container App y las variables las referencian con `secretref:`. El valor queda oculto al inspeccionar la configuración.

**Réplicas.** Los cinco servicios tienen `--min-replicas 1` para que no escalen a cero y la primera petición no tarde en despertar el contenedor.

---

## Seguridad de claves

- Las contraseñas reales no están publicadas en este repositorio
- Los valores sensibles se configuran como secretos en los Container Apps
- `CLAVES_AZURE_EJEMPLO.txt` documenta la estructura con marcadores de posición
- `MEMORIA_COMANDOS_AZURE.txt` contiene los comandos ejecutados, con los secretos reemplazados

---

## Eliminación de los recursos de Azure

Los servicios permanecerán disponibles hasta el **domingo 13 de septiembre de 2026**. Después se eliminarán para evitar consumo de créditos.

az group delete --name rg-pacientes-aa --yes --no-wait


Al eliminar el Resource Group se borran todos los recursos asociados: el registro de imágenes, el servidor SQL con sus bases, el entorno y los cinco Container Apps.

Verificación:

az group list --output table


---

## Estructura del repositorio

DIST-4BM-AA-OAuthJWT-Azure-Bonilla-Matias/
├── ApiGateway/ Proxy inverso con YARP
├── OAuthJWT/ Servicio de autenticación JWT
├── Pacientes.Api/ Microservicio de pacientes
├── HistorialClinico.Api/ Microservicio de historiales
├── BaseDatos/
│ ├── PacientesDB.sql Script local
│ ├── HistorialClinicoDB.sql Script local
│ ├── Azure_PacientesDB.sql Script para Azure SQL
│ └── Azure_HistorialClinicoDB.sql Script para Azure SQL
├── docker-compose.yml Orquestación de los cinco servicios
├── CLAVES_AZURE_EJEMPLO.txt Estructura de credenciales
├── MEMORIA_COMANDOS_AZURE.txt Comandos del despliegue
└── README.md