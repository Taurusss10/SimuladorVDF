using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>Tipo de elemento montado sobre el tablero.</summary>
    public enum VdfElementType
    {
        /// <summary>Bornera hembra tipo banana.</summary>
        Borne,
        /// <summary>Indicador luminoso (piloto).</summary>
        PilotoLed,
        /// <summary>Pulsador rasante.</summary>
        Pulsador,
        PulsadorHongo,
        /// <summary>Cuerpo del variador V1000, con hueco para pantalla y teclado.</summary>
        Variador,
        /// <summary>Bloque genérico: breaker, placas de regleta, cajas.</summary>
        Bloque,
        /// <summary>Solo texto serigrafiado sobre el tablero.</summary>
        Etiqueta,
        /// <summary>
        /// Rectángulo de línea fina serigrafiado, de los que agrupan una sección del tablero.
        ///
        /// En el módulo real son lo que organiza la placa: cada bloque de función va dentro de
        /// su caja azul. Sin ellos el tablero se ve como un reparto suelto de botones y bornes,
        /// y era la diferencia más visible al poner las dos imágenes una al lado de la otra.
        /// </summary>
        Recuadro
    }

    /// <summary>
    /// Un elemento del tablero, en coordenadas de panel: metros desde la esquina
    /// inferior izquierda de la placa, mirando el tablero de frente.
    ///
    /// Es una clase serializable y no un MonoBehaviour a propósito: el layout es DATO.
    /// Cuando lleguen las fotos reales del módulo solo hay que mover números en el
    /// asset, sin tocar una línea de código ni rehacer la escena.
    /// </summary>
    [System.Serializable]
    public class VdfPanelElement
    {
        [Tooltip("Identificador único. Es la clave que usa el grafo de conexiones (E4).")]
        public string Id;

        [Tooltip("Texto serigrafiado. Puede repetirse entre elementos distintos (p. ej. 'AC').")]
        public string Etiqueta;

        public VdfElementType Tipo = VdfElementType.Borne;

        [Tooltip("Metros desde la esquina inferior izquierda del tablero.")]
        public Vector2 Posicion;

        [Tooltip("Ancho y alto en metros. En elementos redondos, X es el diámetro.")]
        public Vector2 Tamano = new Vector2(0.022f, 0.022f);

        [Tooltip("Cuánto sobresale del tablero, en metros.")]
        public float Profundidad = 0.015f;

        [Tooltip("Z de la superficie sobre la que va montado, en metros y negativo hacia el " +
                 "usuario. 0 = directamente sobre la placa. Los bornes de la regleta van " +
                 "sobre su propia plancha, así que su etiqueta también debe ir encima de ella.")]
        public float ZMontaje = 0f;

        public Color Color = Color.gray;

        [Tooltip("Agrupación lógica, para la guía de prácticas y para depurar el layout.")]
        public string Grupo = "";

        [Tooltip("Si es false, no se dibuja la etiqueta de texto (p. ej. en bloques grandes).")]
        public bool MostrarEtiqueta = true;

        /// <summary>
        /// Si el rótulo del componente va ENCIMA en vez de debajo.
        ///
        /// En el módulo real los nombres de los bornes están serigrafiados arriba de su
        /// agujero: la regleta tiene la fila de texto a 20 mm por encima de la fila de
        /// bananas, y lo mismo pasa con R/L1, S/L2 y la salida a motor. El constructor los
        /// ponía siempre debajo, que es lo razonable para un pulsador pero deja la regleta
        /// al revés que la foto, con cada nombre pegado al borne de la fila de abajo.
        /// </summary>
        public bool EtiquetaArriba = false;

        /// <summary>
        /// Separación del rótulo al centro del componente, en metros. Con 0 se calcula a
        /// partir del tamaño y de lo que sobresale la pieza.
        ///
        /// Hace falta por la regleta: el cálculo automático deja los nombres a 14,5 mm del
        /// agujero y en el módulo real están a 20. Son seis milímetros, pero multiplicados
        /// por los treinta rótulos de las dos filas es lo que mas se nota al poner la
        /// captura al lado de la foto.
        /// </summary>
        public float SeparacionEtiqueta = 0f;

        public VdfPanelElement() { }

        public VdfPanelElement(string id, string etiqueta, VdfElementType tipo,
                               float x, float y, Color color, string grupo)
        {
            Id = id;
            Etiqueta = etiqueta;
            Tipo = tipo;
            Posicion = new Vector2(x, y);
            Color = color;
            Grupo = grupo;
            Tamano = TamanoPorDefecto(tipo);
            Profundidad = ProfundidadPorDefecto(tipo);
        }

        public static Vector2 TamanoPorDefecto(VdfElementType tipo)
        {
            switch (tipo)
            {
                case VdfElementType.Borne: return new Vector2(0.014f, 0.014f);
                // 26 mm de cabeza y 29 de embellecedor con el collar. Estaban en 30, que
                // con el collar daban 34: el detector de bananas de la foto los dejaba
                // fuera por tamano mientras que a los del modulo real los cogia.
                case VdfElementType.PilotoLed: return new Vector2(0.026f, 0.026f);
                case VdfElementType.Pulsador: return new Vector2(0.026f, 0.026f);
                case VdfElementType.PulsadorHongo: return new Vector2(0.040f, 0.040f);
                case VdfElementType.Variador: return new Vector2(0.068f, 0.128f);
                case VdfElementType.Bloque: return new Vector2(0.053f, 0.085f);
                default: return new Vector2(0.100f, 0.020f);
            }
        }

        public static float ProfundidadPorDefecto(VdfElementType tipo)
        {
            switch (tipo)
            {
                case VdfElementType.Borne: return 0.010f;
                case VdfElementType.PilotoLed: return 0.010f;
                case VdfElementType.Pulsador: return 0.014f;
                case VdfElementType.PulsadorHongo: return 0.022f;
                // Fondo real del V1000 de esta gama: de 76 a 118 mm segun modelo.
                case VdfElementType.Variador: return 0.076f;
                // Un magnetotermico mide 70 mm de fondo, pero va montado sobre carril
                // DENTRO del armario: por delante de la chapa solo asoman unos 30. Con los
                // 70 completos el breaker tapaba su propia etiqueta serigrafiada.
                case VdfElementType.Bloque: return 0.030f;
                default: return 0.001f;
            }
        }

        /// <summary>¿Este elemento admite que se le enchufe un jumper? (E4)</summary>
        public bool EsConectable { get { return Tipo == VdfElementType.Borne; } }
    }

    /// <summary>
    /// Layout completo del tablero, como asset editable.
    /// Se ajustará con fotos reales del módulo: por eso vive en un ScriptableObject
    /// y no incrustado en una escena.
    /// </summary>
    [CreateAssetMenu(fileName = "VdfPanelLayout", menuName = "VDF/Layout de tablero")]
    public class VdfPanelLayout : ScriptableObject
    {
        [Header("Placa")]
        [Tooltip("Ancho del tablero en metros.")]
        public float Ancho = 1.10f;

        [Tooltip("Alto del tablero en metros.")]
        public float Alto = 0.85f;

        [Tooltip("Grosor de la placa en metros.")]
        public float Grosor = 0.030f;

        public Color ColorTablero = new Color(0.78f, 0.80f, 0.82f);

        [Header("Elementos")]
        public List<VdfPanelElement> Elementos = new List<VdfPanelElement>();

        /// <summary>Busca un elemento por Id. Devuelve null si no existe.</summary>
        public VdfPanelElement Buscar(string id)
        {
            if (Elementos == null) return null;
            for (int i = 0; i < Elementos.Count; i++)
                if (Elementos[i] != null && Elementos[i].Id == id)
                    return Elementos[i];
            return null;
        }

        /// <summary>Todos los bornes, que son los únicos puntos conectables.</summary>
        public List<VdfPanelElement> Bornes()
        {
            var r = new List<VdfPanelElement>();
            if (Elementos == null) return r;
            for (int i = 0; i < Elementos.Count; i++)
                if (Elementos[i] != null && Elementos[i].EsConectable)
                    r.Add(Elementos[i]);
            return r;
        }

        /// <summary>
        /// Comprueba la coherencia del layout: ids duplicados, elementos fuera de la placa
        /// y solapamientos. Se usa en los tests de E3.
        /// </summary>
        public List<string> Validar()
        {
            var problemas = new List<string>();
            if (Elementos == null) return problemas;

            var vistos = new HashSet<string>();
            for (int i = 0; i < Elementos.Count; i++)
            {
                var e = Elementos[i];
                if (e == null) { problemas.Add("Elemento nulo en el indice " + i); continue; }

                if (string.IsNullOrEmpty(e.Id))
                    problemas.Add("Elemento sin Id en el indice " + i);
                else if (!vistos.Add(e.Id))
                    problemas.Add("Id duplicado: " + e.Id);

                float mitadX = e.Tamano.x * 0.5f;
                float mitadY = e.Tamano.y * 0.5f;
                if (e.Posicion.x - mitadX < -0.001f || e.Posicion.x + mitadX > Ancho + 0.001f ||
                    e.Posicion.y - mitadY < -0.001f || e.Posicion.y + mitadY > Alto + 0.001f)
                {
                    problemas.Add(e.Id + " se sale de la placa (pos " + e.Posicion + ", tam " + e.Tamano + ")");
                }
            }

            // Solapamientos entre elementos fisicos (las etiquetas pueden solaparse sin problema).
            for (int i = 0; i < Elementos.Count; i++)
            {
                var a = Elementos[i];
                if (a == null || a.Tipo == VdfElementType.Etiqueta) continue;

                for (int j = i + 1; j < Elementos.Count; j++)
                {
                    var b = Elementos[j];
                    if (b == null || b.Tipo == VdfElementType.Etiqueta) continue;

                    // El variador y el breaker son cajas que pueden contener otros elementos
                    // decorativos; solo nos interesan los choques entre cosas conectables/pulsables.
                    if (a.Tipo == VdfElementType.Variador || b.Tipo == VdfElementType.Variador) continue;
                    if (a.Tipo == VdfElementType.Bloque || b.Tipo == VdfElementType.Bloque) continue;

                    // Un recuadro ENVUELVE a sus piezas: para eso está. Contarlo como
                    // choque hacía que cada borne de la regleta saliera como problema, 63 de
                    // golpe, y entre ese ruido no se habría visto un solapamiento de verdad.
                    if (a.Tipo == VdfElementType.Recuadro || b.Tipo == VdfElementType.Recuadro) continue;

                    float solapeX = (a.Tamano.x + b.Tamano.x) * 0.5f - Mathf.Abs(a.Posicion.x - b.Posicion.x);
                    float solapeY = (a.Tamano.y + b.Tamano.y) * 0.5f - Mathf.Abs(a.Posicion.y - b.Posicion.y);
                    if (solapeX > 0.0005f && solapeY > 0.0005f)
                        problemas.Add("Solapan " + a.Id + " y " + b.Id);
                }
            }

            return problemas;
        }
    }
}
