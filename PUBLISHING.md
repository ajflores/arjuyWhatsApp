# Publicar una nueva versión — ArjuyWhatsApp

Guía para vos (mantenedor), no para quien consume la librería — eso está en `MANUAL.md`. Seguí
estos pasos cada vez que cambies algo en `ArjuyWhatsApp/` y quieras que la gente que ya la instaló
pueda actualizar.

---

## 0. Antes de arrancar — checklist rápido

- [ ] ¿El cambio rompe algo de la API pública (renombraste un método, cambiaste una firma)? Si sí, es versión **major**.
- [ ] ¿Agregaste algo nuevo sin romper nada existente (una tool, un método, un overload)? Versión **minor**.
- [ ] ¿Es solo un fix (como el bug del `9` en `ArgentinaPhoneNumberNormalizer`)? Versión **patch**.
- [ ] ¿Corren los 130 tests?
- [ ] ¿Tu API key de NuGet sigue vigente? (la creaste con expiración a 30 días — ver sección 4)

---

## 1. Versionado semántico (`MAJOR.MINOR.PATCH`)

El número vive en un solo lugar: `ArjuyWhatsApp/ArjuyWhatsApp.csproj`, tag `<Version>`.

```xml
<Version>1.0.0</Version>
```

- **PATCH** (`1.0.0` → `1.0.1`): arreglaste un bug, no agregaste ni sacaste nada de la API pública.
  Ejemplo real: el fix de `ArgentinaPhoneNumberNormalizer` hubiera sido `1.0.1`.
- **MINOR** (`1.0.1` → `1.1.0`): agregaste algo nuevo (un método, un tipo, un parámetro opcional
  con default) sin romper código existente que ya usa la librería.
- **MAJOR** (`1.1.0` → `2.0.0`): rompiste algo — renombraste/sacaste un método público, cambiaste
  una firma de forma incompatible, cambiaste el comportamiento de algo de forma que el código
  existente deja de andar igual.

No hace falta instalar nada para decidir esto — es criterio tuyo leyendo el diff.

---

## 2. Pasos para publicar

### 2.1. Corré los tests

```powershell
cd "C:\Users\floaj\OneDrive\Documentos\Desarrollo\arjuyWhatsApp"
dotnet test ArjuyWhatsApp.Tests/ArjuyWhatsApp.Tests.csproj
```

Si algo falla, no sigas — arreglalo primero.

### 2.2. Subí la versión en el `.csproj`

Editá `ArjuyWhatsApp/ArjuyWhatsApp.csproj`, tag `<Version>`, con el número que corresponda según
la sección 1.

### 2.3. Commiteá y pusheá el cambio de versión

```powershell
git add ArjuyWhatsApp/ArjuyWhatsApp.csproj
git commit -m "chore: bump version to X.Y.Z"
git push origin master
```

(Además de cualquier otro cambio de código que estés publicando en esta versión — el bump de
versión puede ir en el mismo commit que el fix/feature, no hace falta que sea aparte.)

### 2.4. Empaquetá

```powershell
dotnet pack ArjuyWhatsApp/ArjuyWhatsApp.csproj -c Release
```

Esto genera `ArjuyWhatsApp\bin\Release\ArjuyWhatsApp.X.Y.Z.nupkg` — fijate que el número en el
nombre del archivo coincida con el que pusiste en el `.csproj` (si no coincide, quedó algo en caché
de un build anterior; borrá `ArjuyWhatsApp\bin\Release` y volvé a empaquetar).

### 2.5. Subí el paquete a NuGet.org

**Desde tu propia terminal** (nunca pegues la API key en un chat con Claude ni en ningún otro
lado):

```powershell
dotnet nuget push ArjuyWhatsApp\bin\Release\ArjuyWhatsApp.X.Y.Z.nupkg --api-key TU_KEY --source https://api.nuget.org/v3/index.json
```

Reemplazá `X.Y.Z` por la versión real y `TU_KEY` por tu API key (ver sección 4 si venció o no la
tenés a mano). Tip para no tipearla nunca en texto plano en el comando: guardala una vez en una
variable de entorno de tu usuario de Windows (`setx NUGET_API_KEY "tu-key"`, en una terminal nueva
después de eso) y usá `--api-key $env:NUGET_API_KEY` en PowerShell.

### 2.6. Tagueá el release en git (opcional pero recomendado)

```powershell
git tag vX.Y.Z
git push origin vX.Y.Z
```

Sirve para poder encontrar después "qué commit exacto es la versión 1.0.1" sin tener que adivinar.

### 2.7. Verificá

Esperá unos minutos a que NuGet indexe, y confirmá en `https://www.nuget.org/packages/ArjuyWhatsApp`
que la versión nueva aparece. Un proyecto de prueba con `dotnet add package ArjuyWhatsApp` debería
traer la última versión automáticamente.

---

## 3. Resumen — los 3 comandos que más vas a repetir

```powershell
cd "C:\Users\floaj\OneDrive\Documentos\Desarrollo\arjuyWhatsApp"
dotnet test ArjuyWhatsApp.Tests/ArjuyWhatsApp.Tests.csproj
dotnet pack ArjuyWhatsApp/ArjuyWhatsApp.csproj -c Release
dotnet nuget push ArjuyWhatsApp\bin\Release\ArjuyWhatsApp.X.Y.Z.nupkg --api-key TU_KEY --source https://api.nuget.org/v3/index.json
```

(Antes de eso, no te olvides de subir el `<Version>` en el `.csproj` y commitear.)

---

## 4. La API key de NuGet

La creaste (`arjuyWhatsApp-publish`) con **expiración a 30 días** — cuando venza, `dotnet nuget
push` te va a devolver un error de autenticación (403). No es un bug, es que la key caducó.

**Cómo generar una nueva:**

1. `nuget.org` → tu usuario (arriba a la derecha) → **API Keys**.
2. `+ Create` → mismo criterio que la primera vez:
   - Key Name: `arjuyWhatsApp-publish` (o `arjuyWhatsApp-publish-2`, lo que sea)
   - Select Scopes: **Push new packages and package versions**
   - Glob Pattern: `ArjuyWhatsApp*`
3. Copiá la key nueva (solo se muestra una vez) y usala en el próximo push.

No hace falta borrar la vieja si ya venció — simplemente deja de funcionar sola.

---

## 5. Qué NO hacer

- No bajes el número de versión ni reuses uno ya publicado — NuGet no te va a dejar subir
  `1.0.0` de nuevo si ya existe (por diseño, para que nadie pise una versión que otro ya instaló).
- No publiques sin correr los tests antes.
- No pegues la API key en ningún chat, issue de GitHub, commit, ni la dejes en un archivo
  versionado — si se te escapa en algún lado, andá a `nuget.org/account/apikeys` y usá **Revoke**
  sobre esa key de inmediato, después generá una nueva.
