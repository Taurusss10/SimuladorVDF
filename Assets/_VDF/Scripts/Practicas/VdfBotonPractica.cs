
using TMPro;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Un botón del menú de prácticas.
    ///
    /// Es geometría con collider, no un Button de UI: así lo puede pulsar el ratón en
    /// escritorio, el rayo del mando en VR y el dedo con un poke, sin que el botón sepa de
    /// cuál se trata. El mismo criterio que siguen las teclas del V1000.
    /// </summary>
    public class VdfBotonPractica : MonoBehaviour
    {
        public VdfMenuPracticas Menu;

        [Tooltip("Índice de la práctica dentro de la lista del menú.")]
        public int Indice;

        [SerializeField] private Renderer _fondo;
        [SerializeField] private TextMeshPro _texto;

        /// <summary>
        /// Qué hacer al pulsarlo, cuando el botón no pertenece al menú de prácticas.
        ///
        /// El mismo botón sirve para el menú y para el panel de diagnóstico; lo único que
        /// cambia es a quién avisa. Antes solo sabía avisar al menú.
        /// </summary>
        public System.Action<int> AlPulsar;

        /// <summary>Punto de entrada único: lo llaman el ratón, el rayo y el dedo.</summary>
        public void Pulsar()
        {
            if (AlPulsar != null) { AlPulsar(Indice); return; }
            if (Menu != null) Menu.Seleccionar(Indice);
        }

        public void Pintar(Color color)
        {
            if (_fondo == null) _fondo = GetComponentInChildren<Renderer>();
            if (_fondo != null)
                _fondo.sharedMaterial = VdfPanelBuilder.ObtenerMaterial(color, 0.25f);
        }

        // OJO: aquí había un OnMouseDown() y NO funcionaba. Con
        // `activeInputHandler = 1` (solo Input System nuevo) Unity no envía los mensajes
        // OnMouseXXX, así que los botones eran decorativos. Quien detecta el clic es
        // VdfMenuPracticas, por raycast, igual que hace VdfJumperController con los bornes.

        public static VdfBotonPractica Construir(Transform padre, int indice, string etiqueta,
                                                 Vector3 posicion, Vector2 tamano,
                                                 Color colorFondo, Color colorTexto,
                                                 float tamanoLetra = 0.013f)
        {
            var raiz = new GameObject("Boton_" + indice);
            raiz.transform.SetParent(padre, false);
            raiz.transform.localPosition = posicion;

            var boton = raiz.AddComponent<VdfBotonPractica>();
            boton.Indice = indice;

            var fondo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fondo.name = "Fondo";
            fondo.transform.SetParent(raiz.transform, false);
            fondo.transform.localScale = new Vector3(tamano.x, tamano.y, 0.008f);

            // El collider se queda: es el blanco de la pulsación. Se hace algo más grueso
            // que la chapa para que acertarle con el rayo no exija puntería.
            var col = fondo.GetComponent<BoxCollider>();
            if (col != null) col.size = new Vector3(1f, 1f, 3f);

            var r = fondo.GetComponent<Renderer>();
            r.sharedMaterial = VdfPanelBuilder.ObtenerMaterial(colorFondo, 0.25f);
            boton._fondo = r;

            var go = new GameObject("Etiqueta");
            go.transform.SetParent(raiz.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.006f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = etiqueta;
            tmp.color = colorTexto;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            // Autoajuste SOLO hacia abajo: el tamaño pedido es el máximo y las etiquetas que
            // no caben encogen hasta un 70 %. Al agrandar las letras para que se leyeran en
            // el visor, las causas largas del panel de diagnóstico se salían del botón.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = tamanoLetra * 10f;
            tmp.fontSizeMin = tamanoLetra * 10f * 0.70f;
            // El x10 es de TextMeshPro: su fontSize no está en metros, y el texto vive en un
            // objeto a escala 1. Sin él la letra sale diez veces más pequeña de lo pedido.
            tmp.fontSize = tamanoLetra * 10f;
            tmp.rectTransform.sizeDelta = new Vector2(tamano.x * 0.96f, tamano.y);
            boton._texto = tmp;

            return boton;
        }
    }
}
