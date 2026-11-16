
using TMPro;
using UnityEngine;

namespace VDF
{
    public class VdfPanelBienvenida : MonoBehaviour
    {
        public const string NombreRaiz = "VDF_PanelBienvenida";

        /// <summary>Índice con el que se distingue su botón de los del menú.</summary>
        public const int IndiceBotonEmpezar = 98;

        [SerializeField] private VdfBotonPractica _boton;

        private void Awake()
        {
            Enganchar();
        }

        /// <summary>Ata el botón de EMPEZAR. Se llama en Awake y al construir el panel.</summary>
        public void Enganchar()
        {
            // El delegado del botón no se serializa, así que hay que volver a atarlo en cada
            // arranque. Mismo motivo que en VdfPanelGuia: lo que vive solo en memoria se
            // pierde al cargar la escena, y el botón aparecería sin hacer nada.
            if (_boton == null)
                foreach (var b in GetComponentsInChildren<VdfBotonPractica>(true))
                    if (b.Indice == IndiceBotonEmpezar) _boton = b;

            if (_boton != null) _boton.AlPulsar = delegate(int _) { Cerrar(); };
        }

        /// <summary>Quita la bienvenida y deja al alumno con el puesto de trabajo.</summary>
        public void Cerrar()
        {
            gameObject.SetActive(false);
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        public static VdfPanelBienvenida Construir(Vector3 posicion, bool enVisor)
        {
            var viejo = GameObject.Find(NombreRaiz);
            if (viejo != null)
            {
                if (Application.isPlaying) Destroy(viejo); else DestroyImmediate(viejo);
            }

            // El panel es MAS PEQUEÑO en escritorio, y no es capricho.
            //
            // En el visor los ojos estan a 1,36 m y el panel se mira a 33 cm: cabe entero y
            // sobra sitio. En escritorio la camara se queda a 0,88 m para encuadrar el
            // tablero, o sea casi a la altura de la encimera, y mucho mas cerca. Con el
            // tamaño de VR pasaban dos cosas a la vez: la parte de abajo del panel quedaba
            // POR DEBAJO de la encimera -a 0,75 m- y por tanto tapada por la propia mesa, de
            // modo que el boton de EMPEZAR no se veia; y si se subia para salvarla, la parte
            // de arriba se salia del cuadro.
            //
            // Encogiendolo cabe holgado por encima de la mesa y el boton queda a la vista.
            float factor = enVisor ? 1f : 0.78f;
            float ancho = 0.58f * factor;
            float alto = 0.44f * factor;

            var raiz = new GameObject(NombreRaiz);
            raiz.transform.position = posicion;
            var panel = raiz.AddComponent<VdfPanelBienvenida>();

            var fondo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fondo.name = "Fondo";
            fondo.transform.SetParent(raiz.transform, false);
            fondo.transform.localScale = new Vector3(ancho, alto, 0.012f);
            var col = fondo.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }
            fondo.GetComponent<Renderer>().sharedMaterial =
                VdfPanelBuilder.ObtenerMaterial(new Color(0.07f, 0.09f, 0.13f), 0.15f);

            float margen = ancho * 0.045f;
            float z = -0.008f;

            // La maqueta va por franjas y sin solaparse: con el primer reparto el boton
            // pisaba las dos ultimas lineas del texto y no se leian.
            float yTitulo = alto * 0.5f - margen - 0.020f * factor;
            float altoBoton = 0.046f * factor;
            float yBoton = -alto * 0.5f + margen + altoBoton * 0.5f;
            float topeCuerpo = yTitulo - 0.030f * factor;
            float baseCuerpo = yBoton + altoBoton * 0.5f + 0.014f * factor;

            Texto(raiz.transform, "Titulo",
                  new Vector3(0f, yTitulo, z),
                  new Vector2(ancho - margen * 2f, 0.040f * factor), 0.0235f * factor,
                  TextAlignmentOptions.Center,
                  "ANTES DE EMPEZAR",
                  new Color(1f, 1f, 1f));

            Texto(raiz.transform, "Cuerpo",
                  new Vector3(0f, (topeCuerpo + baseCuerpo) * 0.5f, z),
                  new Vector2(ancho - margen * 2f, topeCuerpo - baseCuerpo), 0.0145f * factor,
                  TextAlignmentOptions.TopLeft,
                  CuerpoDeAyuda(enVisor),
                  new Color(0.80f, 0.84f, 0.90f));

            panel._boton = VdfBotonPractica.Construir(
                raiz.transform, IndiceBotonEmpezar,
                enVisor ? "APUNTA AQUÍ Y APRIETA EL GATILLO" : "EMPEZAR",
                new Vector3(0f, yBoton, z),
                new Vector2(ancho * 0.60f, altoBoton),
                new Color(0.16f, 0.52f, 0.26f), Color.white, 0.0155f * factor);

            // Llamada directa y no SendMessage("Awake"): mandar Awake a mano en el editor,
            // fuera de Play, daba «Assertion failed: ShouldRunBehaviour()» una vez por
            // escena reconstruida.
            panel.Enganchar();
            return panel;
        }

        private static string CuerpoDeAyuda(bool enVisor)
        {
            char nl = (char)10;
            var sb = new System.Text.StringBuilder();

            if (enVisor)
            {
                sb.Append("Un variador Yaskawa V1000, un PLC Siemens y un motor trifásico.").Append(nl).Append(nl);
                sb.Append("<b>Con el mando:</b>").Append(nl);
                sb.Append("   Señalar o pulsar algo ......... apunta y aprieta el <b>gatillo</b>").Append(nl);
                sb.Append("   Coger un cable ................ <b>gatillo lateral</b>, el del dedo medio").Append(nl);
                sb.Append("   Enchufarlo .................... acércalo al borne, encaja solo").Append(nl);
                sb.Append("   Quitarlo ...................... cógelo y suéltalo en el aire").Append(nl);
                sb.Append("   Moverte ....................... joystick y apunta a un disco del suelo").Append(nl);
                sb.Append("   Variador (apuntándolo) ...... <b>A</b> ENTER · <b>B</b> RESET · joystick ▲▼").Append(nl);
                sb.Append("   Lupa ............................ <b>X</b> la muestra · <b>Y</b> cambia el aumento").Append(nl).Append(nl);
                sb.Append("<b>A tu izquierda</b> la guía te dice qué hacer en cada paso.").Append(nl);
                sb.Append("<b>A tu derecha</b> eliges una de las cinco prácticas.");
            }
            else
            {
                sb.Append("Un variador Yaskawa V1000, un PLC Siemens y un motor trifásico.").Append(nl).Append(nl);
                sb.Append("<b>Con el ratón:</b>").Append(nl);
                sb.Append("   Señalar o conectar ............ <b>clic izquierdo</b>").Append(nl);
                sb.Append("   Quitar un cable ............... <b>clic derecho</b> encima").Append(nl);
                sb.Append("   Girar alrededor ............... arrastra con el <b>botón central</b>").Append(nl);
                sb.Append("   Acercarte ..................... <b>rueda</b>").Append(nl).Append(nl);
                sb.Append("<b>A la izquierda</b> la guía te dice qué hacer en cada paso.").Append(nl);
                sb.Append("<b>A la derecha</b> eliges una de las cinco prácticas.");
            }
            return sb.ToString();
        }

        private static TextMeshPro Texto(Transform padre, string nombre, Vector3 pos,
                                         Vector2 caja, float tamano,
                                         TextAlignmentOptions alineacion,
                                         string contenido, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = contenido;
            tmp.color = color;
            tmp.alignment = alineacion;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            tmp.enableAutoSizing = false;
            tmp.fontSize = tamano * 10f;
            tmp.rectTransform.sizeDelta = caja;
            return tmp;
        }
    }
}
