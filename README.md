# Simulador del módulo VDF

Simulador en realidad virtual y en computador del módulo didáctico de variador de frecuencia de
la Universidad de Pamplona: un variador Yaskawa V1000 gobernado por un PLC Siemens S7-1200. Ofrece
cinco prácticas guiadas (reconocimiento del módulo, configuración del variador, cableado,
operación y seguridad y diagnóstico de averías) que se realizan con un visor Meta Quest o con
ratón y teclado en Windows.

## Requisitos

- Unity 6.6 (6000.6.7f1), instalado desde Unity Hub con licencia Personal.
- Para generar el APK de Meta Quest: el módulo Android Build Support, con OpenJDK y Android SDK & NDK.

## Abrir el proyecto

1. En Unity Hub, *Add project from disk* y elegir esta carpeta. La primera apertura tarda varios
   minutos mientras Unity importa los recursos.
2. Las escenas están en `Assets/_VDF/Scenes/`: `VDF_Desktop` (computador) y `VDF_VR` (realidad
   virtual).
3. Las pruebas automáticas se ejecutan desde *Window → General → Test Runner*.

## Estructura

| Carpeta | Contenido |
| --- | --- |
| `Assets/_VDF/Scripts` | Código del simulador: variador, PLC, Ladder, cableado, prácticas, averías, tablero e interacción XR |
| `Assets/_VDF/Editor` | Menús del editor (VDF → …) para reconstruir las escenas y preparar el proyecto para Quest |
| `Assets/_VDF/Tests` | Pruebas automáticas (EditMode y PlayMode) |
| `Assets/_VDF/Textures` | Texturas; las de materiales son de ambientCG con licencia CC0 (ver `Textures/CC0/LICENCIA.txt`) |

## Generar los instalables

- **Windows:** *File → Build Profiles*, plataforma Windows, con `VDF_Desktop` primera en la lista
  de escenas, y *Build* sobre una carpeta vacía.
- **Meta Quest:** *File → Build Profiles*, plataforma Android y *Switch Platform*; después
  *VDF → Preparar el proyecto para Quest*, con `VDF_VR` primera en la lista de escenas, y *Build*.
  Antes de cada versión nueva conviene ejecutar *VDF → Reconstruir las dos escenas* y comprobar
  que las pruebas están en verde.
