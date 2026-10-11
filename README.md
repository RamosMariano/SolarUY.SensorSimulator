# SolarUY.SensorSimulator

Simulador (caja negra) del **sensor ARM + inversor** de cada nodo de un parque solar. Implementa la spec `7_simulador_sensor.md`.

El sistema de gestión (`SolarUY.Web`) lo consulta por HTTP como si fuera hardware real. El simulador **no conoce** la base de datos, SignalR ni el dominio de SolarUY: solo expone el contrato de la sección 3 de la spec.

## Contenido del repositorio

| Carpeta / archivo | Qué es |
|---|---|
| `src/SolarUY.SensorSimulator` | El simulador (ASP.NET Core Minimal API) |
| `tests/SolarUY.SensorSimulator.Tests` | Tests unitarios y de integración (xUnit) |
| `docs/7_simulador_sensor.md` | Especificación y contrato de la API |
| `postman/` | Colección de Postman con todas las peticiones |
| `requests.http` | Las mismas peticiones para la extensión REST Client de VS Code |

## Requisitos

- .NET 10 SDK (`dotnet --version` debe mostrar 10.x). Si tenés otra versión, cambiá `<TargetFramework>` en los dos `.csproj` (por ejemplo `net8.0`) y la versión de `Microsoft.AspNetCore.Mvc.Testing` en el proyecto de tests (`8.0.*`). El runtime instalado tiene que coincidir con el `TargetFramework`.
- VS Code con la extensión C# (o C# Dev Kit). Opcional: "REST Client" para usar `requests.http`, o Postman para importar `postman/SolarUY-SensorSimulator.postman_collection.json`.

## Primeros pasos (PowerShell, desde esta carpeta)

```powershell
# 1. Compilar
dotnet build

# 2. Probar (unitarios + integración de los endpoints)
dotnet test

# 3. Levantar el simulador en http://localhost:5100
dotnet run --project src/SolarUY.SensorSimulator
```

En otra terminal:

```powershell
Invoke-RestMethod http://localhost:5100/sensores/ARM-001/estado
Invoke-RestMethod http://localhost:5100/simulador/inventario
Invoke-RestMethod -Method Post "http://localhost:5100/simulador/fallas/ARM-001/P02?tipo=sobretemperatura"
```

Es un mock para pruebas: **no tiene autenticación** (no hay API key). No lo expongas fuera de tu máquina o red local.

## API

**Contrato del sensor** (lo que consume el sistema de gestión)

| Ruta | Efecto | Respuestas |
|---|---|---|
| `GET /sensores/{hw}/estado` | Lecturas de todos los paneles del nodo | 200, 404 |
| `POST /sensores/{hw}/paneles/{codigo}/activar` | Reactiva el panel | 200, 404, 409 |
| `POST /sensores/{hw}/paneles/{codigo}/desactivar` | Panel inactivo, lecturas en cero | 200, 404, 409 |
| `POST /sensores/{hw}/volcado/activar` | Activa el volcado a la red | 200, 404, 409 |
| `POST /sensores/{hw}/volcado/desactivar` | Desactiva el volcado a la red | 200, 404, 409 |

**Control del simulador** (solo existe en el mock, para demos)

| Ruta | Efecto |
|---|---|
| `GET /simulador/inventario` | Sensores y paneles simulados (copiá de acá los códigos al dar de alta en el CRUD) |
| `POST /simulador/sensores` `{ "identificadorHardwareARM", "cantidadPaneles", "potenciaNominalW" }` | Agrega un nodo en caliente (201, 400, 409) |
| `DELETE /simulador/sensores/{hw}` | Quita un sensor (204, 404) |
| `POST /simulador/fallas/{hw}/{codigo}?tipo=sobretemperatura\|caidaPotencia` | Fuerza una falla hasta que se la restablece |
| `DELETE /simulador/fallas/{hw}/{codigo}` | Restablece el panel |

## Cuántos nodos y paneles

En `appsettings.json`, sección `Simulador`:

- Topología uniforme: `NodosIniciales` × `PanelesPorNodo` (por defecto 3 × 5 → `ARM-001`…`ARM-003`, paneles `P01`…`P05`).
- Topología no uniforme: lista `Nodos`, por ejemplo `"Nodos": [ { "id": "ARM-001", "paneles": 6 }, { "id": "ARM-002", "paneles": 4, "potenciaNominalW": 450 } ]`. Si tiene elementos, reemplaza a la uniforme.
- En caliente: `POST /simulador/sensores`.

## Mapa del código (para entenderlo y defenderlo)

| Archivo | Responsabilidad |
|---|---|
| `Program.cs` | Registra servicios y arma el pipeline |
| `Configuration/SimuladorOptions.cs` | Opciones tipadas de la sección `Simulador` |
| `Endpoints/SensorEndpoints.cs` | Contrato del sensor: estado y órdenes |
| `Endpoints/ControlEndpoints.cs` | Alta/baja de nodos y fallas forzadas, con validación de entrada |
| `Simulacion/GeneradorLectura.cs` | Curva solar, ruido, temperatura y fallas. Lógica pura y probable |
| `Simulacion/SensorSimulado.cs` | Un sensor con sus paneles; protege su estado con `lock` |
| `Simulacion/SensorStore.cs` | Registro en memoria de sensores (singleton, no distingue mayúsculas) |
| `Simulacion/PanelSimulado.cs`, `TipoFalla.cs`, `ZonaHoraria.cs` | Modelo del panel, tipos de falla, resolución de zona horaria |
| `Modelos/Dtos.cs` | Contrato JSON (camelCase), alineado con `LecturaTelemetria` |

## Decisiones y por qué

- **El estado se calcula al responder `/estado`.** No hay un Worker generando datos de fondo: la lectura sale de la curva solar según la hora de Montevideo. Menos piezas y el sistema siempre recibe un valor fresco.
- **El simulador no envía `EstadoDetectado`.** Un sensor real solo mide. Diagnosticar (75 °C, caída de potencia) es responsabilidad del dominio del sistema, donde se puede probar con tests.
- **`GeneradorLectura` recibe hora y `Random` por parámetro.** Así los tests son deterministas y no dependen del reloj del equipo.
- **`TimeProvider` inyectado.** Los tests de integración fijan la hora en el mediodía solar, y la potencia máxima es predecible.
- **`lock` por sensor.** ASP.NET Core atiende requests en paralelo y el estado (paneles activos, fallas, volcado) es mutable.
- **Panel desactivado = lecturas en cero y no entra en fallas aleatorias.** Coincide con la regla del sistema de excluirlo del promedio del nodo.
- **Falla de sobretemperatura siempre > 75 °C**, incluso de noche, para que la falla forzada sea demostrable a cualquier hora.
- **Sin autenticación**, a propósito: es un mock local. Si más adelante se quiere proteger, se agrega un middleware que valide un header `X-Api-Key`.
- **Validación en la API de control:** el identificador viaja en la URL, así que se limita a letras, números, `-` y `_`; los paneles a 1–200.

## Limitaciones conocidas

- El estado vive en memoria: al reiniciar, vuelve a la configuración inicial.
- Un solo proceso atiende a todos los sensores. En un parque real cada sensor tendría su propia dirección (`SensorDispositivo.UrlBase` en el sistema).
- Solo HTTP en desarrollo (`http://localhost:5100`). Para HTTPS habría que configurar el certificado de desarrollo.
- El voltaje se mantiene en 30–32 V incluso de noche (sigue la spec); un panel real caería a casi 0 V sin luz.

## Tests

`dotnet test` ejecuta:

- **Unitarios:** curva solar, rangos de potencia/voltaje/temperatura, fallas forzadas y aleatorias, órdenes, registro de sensores.
- **Integración:** levantan el simulador real en memoria (`WebApplicationFactory`) y prueban cada endpoint: códigos 200/201/204/400/404/409 y forma del JSON.

Cobertura (opcional): `dotnet test --collect:"XPlat Code Coverage"`.
