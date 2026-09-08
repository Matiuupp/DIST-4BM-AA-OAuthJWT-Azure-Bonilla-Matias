# Arquitectura distribuida segura con OAuthJWT y despliegue en Azure

Sistema de microservicios para la gestión de pacientes e historiales clínicos, desarrollado con .NET 10, Entity Framework Core, SQL Server, RabbitMQ, Docker y un API Gateway con YARP, protegido mediante autenticación JWT y desplegado en Microsoft Azure.

**Materia:** Aplicaciones Distribuidas
**Curso:** Cuarto "B" Matutino - Desarrollo de Software
**Estudiante:** Matías Bonilla
**Tema asignado:** Pacientes e Historial Clínico

---

## Servicios desplegados en Azure

Todos los servicios están publicados y accesibles desde internet.

| Servicio | URL |
|---|---|
| **API Gateway (entrada principal)** | https://apigateway.mangorock-874cd57f.centralus.azurecontainerapps.io |
| OAuthJWT (Swagger) | https://oauthjwt.mangorock-874cd57f.centralus.azurecontainerapps.io/swagger |
| Pacientes.Api (Swagger) | https://pacientes.mangorock-874cd57f.centralus.azurecontainerapps.io/swagger |
| HistorialClinico.Api (Swagger) | https://historial.mangorock-874cd57f.centralus.azurecontainerapps.io/swagger |

RabbitMQ está desplegado con acceso interno únicamente, por seguridad: solo los microservicios dentro del entorno pueden alcanzarlo.

> **Nota:** al entrar a la raíz de cualquier servicio aparece un error 404. Es el comportamiento esperado, porque los endpoints viven bajo `/api/...` y la documentación bajo `/swagger`.

---

## Cómo probar el sistema en 3 pasos

### 1. Comprobar que la protección funciona

GET https://apigateway.mangorock-874cd57f.centralus.azurecontainerapps.io/api/Pacientes


Sin enviar ningún token, la respuesta es **401 Unauthorized**.

### 2. Obtener un token JWT

POST https://apigateway.mangorock-874cd57f.centralus.azurecontainerapps.io/api/Auth/login
Content-Type: application/json

{
"usuario": "admin",
"password": "1234"
}


La respuesta contiene el token, el usuario y su rol:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "usuario": "admin",
  "rol": "Administrador"
}
```

### 3. Repetir la petición con el token

GET https://apigateway.mangorock-874cd57f.centralus.azurecontainerapps.io/api/Pacientes
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...


Ahora responde **200 OK** con la lista de pacientes.

El formato del header es la palabra `Bearer`, un espacio, y el token. Sin ese prefijo la petición falla aunque el token sea válido.

### Usuarios disponibles

| Usuario | Contraseña | Rol | Permisos |
|---|---|---|---|
| admin | 1234 | Administrador | Consultar, crear, editar y eliminar |
| medico | 1234 | Usuario | Solo consultar |

Si se inicia sesión como `medico` y se intenta crear un paciente, la respuesta es **403 Forbidden**: el token es válido, pero el rol no tiene permiso. Es la diferencia entre autenticación (quién eres) y autorización (qué puedes hacer).

---

## Arquitectura

El sistema está formado por cinco componentes independientes:

Cliente / Postman
|
v
API Gateway ──────────────┐
| |
┌────┴────┬──────────┐ |
v v v v
OAuthJWT Pacientes Historial
| ^
└─RabbitMQ─┘

    Azure SQL Database
  (PacientesDB · HistorialDB)

| Componente | Responsabilidad |
|---|---|
| **API Gateway** | Punto único de entrada. Enruta las peticiones al servicio correspondiente usando YARP. No valida tokens: solo reenvía. |
| **OAuthJWT** | Servicio independiente de autenticación. Recibe usuario y contraseña, y emite el token JWT firmado. No tiene base de datos. |
| **Pacientes.Api** | CRUD de pacientes. Valida el token y publica eventos en RabbitMQ. |
| **HistorialClinico.Api** | CRUD de historiales clínicos. Valida el token y consume los eventos de RabbitMQ. |
| **RabbitMQ** | Transporta los mensajes entre los dos microservicios de forma asíncrona. |

### El servicio OAuthJWT

En las prácticas de clase, el microservicio de negocio era también el emisor del token. En esta actividad esa responsabilidad se separó en un servicio propio.

La ventaja es que la autenticación deja de estar acoplada a un microservicio concreto: si mañana se agrega un tercero, todos piden el token al mismo sitio en vez de que cada uno tenga su propio login.

**Los tres servicios comparten exactamente la misma `Key`, `Issuer` y `Audience`.** Esto es indispensable: OAuthJWT firma el token con esa clave y los microservicios verifican la firma con la misma. Si alguno de los tres valores difiere aunque sea en una letra, el token se rechaza aunque sea legítimo.

Los parámetros configurados son:

| Parámetro | Valor | Para qué sirve |
|---|---|---|
| Key | (secreta) | Clave con la que se firma. Impide falsificar tokens. |
| Issuer | OAuthJWT | Quién emitió el token. |
| Audience | MicroserviciosPacientes | Para quién está destinado. |
| ExpireMinutes | 60 | Cuánto tiempo es válido. |

Un JWT no es secreto, es **firmado**. Cualquiera puede leer su contenido, pero nadie puede modificarlo sin conocer la clave. Por eso los claims llevan el usuario y el rol, nunca la contraseña.

---

## Base de datos

### Por qué son dos bases separadas

Cada microservicio tiene su propia base de datos: `PacientesDB` e `HistorialDB`. Es un principio de la arquitectura de microservicios — si compartieran base, la separación sería solo aparente.

### La relación 1:N sin llave foránea

Un paciente puede tener varios historiales clínicos. Pero como las tablas viven en bases distintas, **SQL Server no permite crear una llave foránea entre ellas**.

La solución es que el campo `hist_paciente_id` guarda el id del paciente como un número normal, sin restricción. La relación existe en la lógica de la aplicación, no en el motor de base de datos.

**La ventaja:** cada servicio puede caerse, actualizarse o cambiar de base sin arrastrar al otro.

**El precio:** la base ya no protege la integridad. Acepta sin problema un historial con un id de paciente que no existe. Esa validación tiene que hacerla el código.

### Estructura de las tablas

**tbl_paciente** (base `PacientesDB`)

| Columna | Tipo | Notas |
|---|---|---|
| pac_id | int IDENTITY | Clave primaria |
| pac_cedula | varchar(10) | UNIQUE. Es texto y no número porque puede empezar en cero |
| pac_nombre | varchar(100) | Obligatorio |
| pac_apellido | varchar(100) | Obligatorio |
| pac_direccion | varchar(200) | Opcional |
| pac_estado | bit | Para el borrado lógico. Por defecto 1 |

**tbl_historialclinico** (base `HistorialDB`)

| Columna | Tipo | Notas |
|---|---|---|
| hist_id | int IDENTITY | Clave primaria |
| hist_paciente_id | int | Id del paciente. **Sin FOREIGN KEY** |
| hist_numero | varchar(20) | UNIQUE. Número de historia clínica visible |
| hist_diagnostico | varchar(500) | Obligatorio |
| hist_tratamiento | varchar(500) | Opcional |
| hist_fecha | datetime | Por defecto la fecha actual |
| hist_estado | bit | Para el borrado lógico. Por defecto 1 |

### Scripts incluidos

La carpeta `BaseDatos/` contiene cuatro archivos, dos por escenario:

| Archivo | Uso |
|---|---|
| `PacientesDB.sql` | Ejecución local, en SQL Server instalado |
| `HistorialClinicoDB.sql` | Ejecución local |
| `Azure_PacientesDB.sql` | Ejecución en Azure SQL |
| `Azure_HistorialClinicoDB.sql` | Ejecución en Azure SQL |

**Por qué hay dos versiones.** Azure SQL tiene tres diferencias respecto a SQL Server local:

1. **No admite `CREATE DATABASE` dentro de un script.** Las bases se crean desde el portal o con Azure CLI.
2. **No admite `USE base`.** Cada conexión está atada a una sola base. Hay que seleccionarla antes de ejecutar, no dentro del script.
3. **No se crea un login adicional.** Se usa `adminsql`, el administrador del servidor lógico, que ya tiene acceso a ambas bases.

Los scripts locales crean además el login `usuario_librosB`, reutilizado de un proyecto anterior para aprovechar la configuración existente.

---

## Comunicación asíncrona con RabbitMQ

| Cola | Publica | Consume | Efecto |
|---|---|---|---|
| `paciente_creado` | Pacientes.Api | HistorialClinico.Api | Crea un historial inicial con número `HC-XXXX` |
| `paciente_desactivado` | Pacientes.Api | HistorialClinico.Api | Desactiva todos los historiales de ese paciente |

### Por qué mensajería y no una llamada HTTP directa

Si `HistorialClinico.Api` estuviera caído, una llamada HTTP fallaría en el acto y el paciente quedaría sin su historial. Con RabbitMQ el mensaje **espera en la cola** hasta que el servicio vuelva a levantarse, y entonces se procesa.

Esto se verificó apagando el consumidor, creando un paciente, y comprobando que el mensaje quedaba en la cola hasta que el servicio volvía.

### Confirmación manual de los mensajes

Los consumidores usan `autoAck: false`. Esto significa que el mensaje **no se borra de la cola al entregarse**, sino cuando el código confirma explícitamente con `BasicAck`, después de haber guardado en la base.

Si algo falla, `BasicNack` con `requeue: true` devuelve el mensaje a la cola para reintentarlo.

Con `autoAck: true` el mensaje se borraría al entregarlo, sin esperar el resultado: si el guardado fallaba después, el mensaje se perdía para siempre. Es la diferencia entre firmar el recibo de un paquete al recibirlo o después de abrirlo y comprobar que llegó bien.

### La cascada manual

Cuando se desactiva un paciente con varios historiales, el consumidor los recorre uno por uno y los desactiva. Si existiera una llave foránea con borrado en cascada, la base lo haría sola. Como no la hay, ese `foreach` es la implementación manual de esa cascada.

---

## El borrado lógico

El `DELETE` no elimina ningún registro. Cambia el campo de estado a `false`, y los listados filtran para mostrar solo los activos.

Se decidió así porque en un sistema de salud la información clínica no puede perderse, y porque si se borrara el paciente sus historiales quedarían huérfanos.

---

## Ejecución local con Docker Compose

### Requisitos

- Docker Desktop
- SQL Server con autenticación mixta y TCP/IP habilitado en el puerto 1433
- SQL Server Management Studio

### Paso 1 — Clonar el repositorio

git clone https://github.com/Matiuupp/DIST-4BM-AA-OAuthJWT-Azure-Bonilla-Matias.git
cd DIST-4BM-AA-OAuthJWT-Azure-Bonilla-Matias


### Paso 2 — Crear las bases de datos

Abrir SQL Server Management Studio con **autenticación de Windows** (hace falta permiso para crear logins) y ejecutar **en este orden**:

1. `BaseDatos/PacientesDB.sql`
2. `BaseDatos/HistorialClinicoDB.sql`

El orden importa: el login se crea en el primer script y el segundo solo lo referencia.

### Paso 3 — Revisar la configuración

En `docker-compose.yml`, verificar que el usuario y la contraseña de SQL Server coincidan con los del script, en los servicios `pacientes` e `historial`.

### Paso 4 — Levantar el sistema

docker compose up --build


La primera vez tarda varios minutos: descarga las imágenes de .NET 10 y compila los cuatro proyectos. Las siguientes veces basta con `docker compose up`.

El sistema está listo cuando aparece `Now listening on: http://[::]:8080` de los cuatro servicios.

### Paso 5 — Probar

| Qué | Dirección local |
|---|---|
| API Gateway | http://localhost:8000 |
| OAuthJWT | http://localhost:8089/swagger |
| Pacientes.Api | http://localhost:8087/swagger |
| HistorialClinico.Api | http://localhost:8088/swagger |
| Panel de RabbitMQ | http://localhost:15672 |

### Paso 6 — Detener

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


El archivo `ApiGateway/ApiGateway.http` contiene todas estas peticiones listas para ejecutar desde Visual Studio.

---

## Despliegue en Azure

Los recursos creados fueron:

| Recurso | Nombre | Región |
|---|---|---|
| Resource Group | rg-pacientes-aa | East US |
| Container Registry | acrpacientesmatias | East US |
| SQL Server | sql-pacientes-matias-26 | Central US |
| Container Apps Environment | env-pacientes | Central US |

El detalle completo de los comandos utilizados está en `MEMORIA_COMANDOS_AZURE.txt`.

### Diferencias entre local y Azure

**Las direcciones entre servicios.** En Docker Compose los servicios se llaman por su nombre de red (`http://pacientes:8080`), porque comparten una red interna. En Container Apps cada servicio tiene su dominio público y el gateway los alcanza por ahí.

**La conexión a la base de datos.** En local se usa `Integrated Security` (el usuario de Windows). Un contenedor no tiene esa sesión, así que se usa usuario y contraseña de SQL, con `Encrypt=True`, que Azure SQL exige.

**Los secretos.** El código no cambia entre escenarios: las variables de entorno de los Container Apps sobrescriben los valores del `appsettings.json` al arrancar.

---

## Seguridad de claves

- Las contraseñas reales no están publicadas en este repositorio.
- Los valores sensibles se configuran como variables de entorno en los Container Apps de Azure.
- El archivo `CLAVES_AZURE_EJEMPLO.txt` documenta la estructura de las credenciales usando marcadores de posición.
- El archivo `MEMORIA_COMANDOS_AZURE.txt` contiene los comandos ejecutados, con los secretos reemplazados.

---

## Eliminación de los recursos de Azure

Los servicios permanecerán disponibles hasta el **domingo 13 de septiembre de 2026**. Después de esa fecha se eliminarán para evitar consumo de créditos.

Para borrar todo:

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