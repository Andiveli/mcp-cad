# Create GitHub issues for bugs found during mcp-cad testing
# Run after: gh auth login

$repo = "Andiveli/mcp-cad"

# Issue 1: sketch_line AddAsTwoPoint fails
gh issue create --repo $repo --title "fix(sketch): sketch_line AddAsTwoPoint fails — should be AddByTwoPoints" --body @"
### Bug Description
`sketch_line` no permite dibujar ninguna línea. El método `AddAsTwoPoint` en `SketchLines` falla contra Inventor real.

### Steps to Reproduce
1. Conectar a Inventor
2. `sketch_create("XY")`
3. `sketch_line(x1=0, y1=0, x2=5, y2=5)`
4. Error: `AddAsTwoPoint` no funciona

### Expected Behavior
La línea se dibuja correctamente entre los dos puntos.

### Actual Behavior
Error COM — `AddAsTwoPoint` no es el método correcto. Debería ser `AddByTwoPoints`.

### Root Cause
`mcp_cad/providers/inventor/sketch.py` línea 130 usa `SketchLines.AddAsTwoPoint(start, end)`. El método correcto de la API de Inventor es `AddByTwoPoints`. Notar que `AddAsTwoPointRectangle` sí funciona porque es otro método en otra colección.

### Fix
Cambiar `AddAsTwoPoint` → `AddByTwoPoints` en `sketch.py:130`.
"@

# Issue 2: revolve CreateRevolveDefinition fails
gh issue create --repo $repo --title "fix(feature): revolve fails — axis not resolved, possible 2025+ API mismatch" --body @"
### Bug Description
`revolve` falla con cualquier valor de `axis`. `CreateRevolveDefinition` nunca llega a ejecutarse correctamente.

### Steps to Reproduce
1. Conectar a Inventor
2. Crear sketch con un círculo y una línea (eje)
3. Intentar `revolve(profile="1", axis="1", angle=360)`
4. Error: falla en `CreateRevolveDefinition`

### Expected Behavior
El perfil se revuelve alrededor del eje especificado.

### Actual Behavior
Error COM en `CreateRevolveDefinition`. Posible causa doble:
1. `axis` se pasa como string crudo pero la API espera un objeto COM (sketch entity). `profile` sí se resuelve con `_resolve_profile()` pero `axis` no.
2. La firma de `CreateRevolveDefinition` puede haber cambiado en Inventor 2025+ (como pasó con extrude).

### Root Cause
`mcp_cad/providers/inventor/feature.py` líneas 274-281:
- `axis` nunca pasa por resolución de entidad
- `CreateRevolveDefinition(resolved, axis)` recibe `axis` como string, no como COM object

### Fix
1. Resolver `axis` como entidad de sketch (similar a `_resolve_profile`)
2. Verificar firma de `CreateRevolveDefinition` en Inventor 2025+
"@

# Issue 3: Need edge inspector tool
gh issue create --repo $repo --title "feat(tools): edge inspector — list edges with geometric properties for fillet/chamfer" --body @"
### Problem Description
Al usar `fillet` o `chamfer` con índices de aristas, no hay forma de saber qué índice corresponde a qué arista geométricamente. El usuario debe adivinar probando índices uno por uno.

Ejemplo real: probando índices 1-9, solo 1, 5, 7, 9 funcionaron. No se sabe cuáles son físicamente.

### Proposed Solution
Crear una herramienta `edge_info` que devuelva para cada arista:
- Índice
- Longitud
- Punto inicial (x, y, z)
- Punto final (x, y, z)
- Caras adyacentes (si es posible)

Esto permitiría al LLM razonar geométricamente: "la arista más larga entre la cara top y front es la índice 5".

### Alternatives Considered
- Selección por ray casting (`SelectByRay`)
- Selección por caras adyacentes ("entre cara top y right")
- Filtro por longitud ("la arista más larga")

### Affected Area
Tools (nueva herramienta de descubrimiento)
"@

# Issue 4: extrude profile auto-resolution skill
gh issue create --repo $repo --title "feat(skills): skill_extrude — auto-resolve profile to avoid LLM guessing" --body @"
### Problem Description
Cada vez que el LLM llama a `extrude`, gasta 2-3 intentos adivinando el formato del parámetro `profile`:
1. Intenta sketch name: `"Sketch1"` → error
2. Intenta `"profile1"` → error
3. Finalmente `"1"` → funciona

El LLM no sabe que `profile` espera un string numérico que indexa `Profiles.Item()`.

### Proposed Solution
Crear una skill `skill_extrude` que:
- Por defecto use `profile="1"` (caso más común: un solo perfil)
- Acepte `sketch_name` opcional y lo resuelva internamente
- O mejor: skill compuesta `crear_extrusion(ancho, alto, profundidad)` que haga sketch_rectangle + extrude en un solo paso

Esto eliminaría 2-3 round-trips del LLM por cada operación de extrusión.

### Alternatives Considered
- Documentar mejor el parámetro profile en la descripción de la tool
- Renombrar el parámetro a `profile_index` para clarificar

### Affected Area
Skills (nueva skill)
"@

Write-Host "Issues created. Review them at https://github.com/$repo/issues"
