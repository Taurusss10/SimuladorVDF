
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Dibuja el diagrama Ladder y lo anima con el estado real del PLC.
    ///
    /// Es **de solo lectura**: no se puede tocar nada. Está para explicar por qué el módulo
    /// hace lo que hace, no para programarlo (editar Ladder está fuera del alcance).
    ///
    /// Como el resto de la interfaz del proyecto, es geometría en el mundo y no un Canvas,
    /// así que funciona igual en VR y en escritorio. Va montado en un atril de pie junto al
    /// puesto, como el tablero de un aula, para no robar sitio en la encimera.
    /// </summary>
    public class VdfLadderPanel : MonoBehaviour
    {
        public const string NombreRaiz = "VDF_PanelLadder";

        [Header("Referencias")]
        public VdfSimulacion Simulacion;

        [Header("Colores")]
        [Tooltip("Un tramo con corriente.")]
        public Color ColorVivo = new Color(0.30f, 0.95f, 0.45f);
        [Tooltip("Un tramo sin corriente. No desaparece: se intuye, como en un visor real.")]
        public Color ColorMuerto = new Color(0.26f, 0.29f, 0.32f);
        public Color ColorFondo = new Color(0.08f, 0.09f, 0.11f);
        public Color ColorTexto = new Color(0.78f, 0.81f, 0.85f);
        public Color ColorTextoVivo = new Color(0.40f, 1f, 0.55f);

        /// <summary>El estado que leen los símbolos. Nunca es null.</summary>
        public VdfEstadoLadder Estado
        {
            get
            {
                if (_estado == null) _estado = new VdfEstadoLadder();
                return _estado;
            }
        }

        private VdfEstadoLadder _estado;

        [SerializeField] private List<VdfLadderPeldanoVista> _vistas = new List<VdfLadderPeldanoVista>();
        [SerializeField] private TextMeshPro _titulo;

        private List<VdfLadderPeldano> _peldanos;

        private void Awake()
        {
            if (Simulacion == null) Simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            if (_peldanos == null) _peldanos = VdfLadderPrograma.Crear();

            if (_vistas == null || _vistas.Count == 0)
                _vistas = new List<VdfLadderPeldanoVista>(GetComponentsInChildren<VdfLadderPeldanoVista>(true));
        }

        private void OnEnable()
        {
            if (Simulacion == null) Simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            if (Simulacion != null) Simulacion.Pulsado += AlPulsar;
        }

        private void OnDisable()
        {
            if (Simulacion != null) Simulacion.Pulsado -= AlPulsar;
        }

        private void AlPulsar(VdfAccionPulsador accion)
        {
            switch (accion)
            {
                case VdfAccionPulsador.Start: AvisarStart(); break;
                case VdfAccionPulsador.Stop: AvisarStop(); break;
                case VdfAccionPulsador.Subir: AvisarSubir(); break;
                case VdfAccionPulsador.Bajar: AvisarBajar(); break;
            }
        }

        /// <summary>Con qué modo de mando están dibujados los peldaños ahora mismo.</summary>
        [SerializeField] private VdfModoMando _modoDibujado = VdfModoMando.Pulsadores;

        /// <summary>
        /// Redibuja el diagrama si el modo de mando no es el que está dibujado. La escena se
        /// guarda con el de pulsadores; con interruptores, los peldaños se rehacen al arrancar
        /// en el mismo sitio, porque sus contactos no son los mismos.
        /// </summary>
        public void UsarMando(VdfModoMando modo)
        {
            if (modo == _modoDibujado && _peldanos != null) return;

            var nuevos = VdfLadderPrograma.Crear(modo);
            if (_vistas != null && _vistas.Count > 0 && _vistas[0] != null && modo != _modoDibujado)
            {
                // Los dos programas no tienen los mismos peldaños (el del laboratorio tiene
                // uno menos), así que se borran todas las vistas y se vuelven a repartir desde
                // la posición de la primera. El tablero se dimensiona para el más largo de los
                // dos (ver Construir), de modo que siempre caben.
                var padre = _vistas[0].transform.parent;
                var origen = _vistas[0].transform.localPosition;
                for (int i = 0; i < _vistas.Count; i++)
                {
                    if (_vistas[i] == null) continue;
                    if (Application.isPlaying) Destroy(_vistas[i].gameObject); else DestroyImmediate(_vistas[i].gameObject);
                }

                _vistas = new List<VdfLadderPeldanoVista>();
                for (int i = 0; i < nuevos.Count; i++)
                {
                    var pos = origen - new Vector3(0f, i * AltoPeldano, 0f);
                    _vistas.Add(VdfLadderPeldanoVista.Construir(padre, nuevos[i], i, pos,
                        AnchoVista, AltoPeldano, this));
                }
            }

            _peldanos = nuevos;
            _modoDibujado = modo;
        }

        /// <summary>Medidas con las que <see cref="Construir"/> reparte los peldaños.</summary>
        private const float AnchoPanel = 0.84f;
        private const float AnchoVista = AnchoPanel - 0.075f;
        private const float AltoPeldano = 0.050f;

        private void Update()
        {
            if (Simulacion != null && Simulacion.ModoMando != _modoDibujado) UsarMando(Simulacion.ModoMando);
            if (Simulacion != null) Estado.Plc = Simulacion.Plc;
            Estado.Tick(Time.deltaTime);
            Refrescar();
        }

        /// <summary>Repinta el diagrama con el estado actual.</summary>
        public void Refrescar()
        {
            if (_peldanos == null) _peldanos = VdfLadderPrograma.Crear();
            if (_vistas == null) return;

            for (int i = 0; i < _vistas.Count && i < _peldanos.Count; i++)
            {
                if (_vistas[i] == null) continue;
                _vistas[i].Pintar(_peldanos[i], Estado, this);
            }
        }

        /// <summary>Avisos de pulsación, para que los pulsadores destellen en el diagrama.</summary>
        public void AvisarStart() { Estado.AvisarStart(); }
        public void AvisarStop() { Estado.AvisarStop(); }
        public void AvisarSubir() { Estado.AvisarSubir(); }
        public void AvisarBajar() { Estado.AvisarBajar(); }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        /// <summary>
        /// Monta el atril con el diagrama. Idempotente.
        /// </summary>
        /// <param name="conPie">
        /// Añade dos patas y una base. Sin ellas el tablero queda flotando en el aire, que
        /// en VR se nota mucho más que en una captura: el ojo busca el apoyo.
        /// </param>
        /// <param name="escala">
        /// Escala uniforme del conjunto. Escalar la raíz escala también el texto, porque
        /// todo se construye en unidades locales: es lo que convierte el atril en un tablero
        /// de aula legible desde el puesto sin tocar un solo tamaño de fuente.
        /// </param>
        public static VdfLadderPanel Construir(Vector3 posicion, float rotacionY = 32f,
                                               float ancho = 0.84f, bool conPie = true,
                                               float escala = 1f)
        {
            var previo = GameObject.Find(NombreRaiz);
            if (previo != null)
            {
                if (Application.isPlaying) Destroy(previo); else DestroyImmediate(previo);
            }

            var peldanos = VdfLadderPrograma.Crear();

            // Alto para el programa más largo de los dos modos de mando: así, al cambiar de
            // modo en caliente, los peldaños nuevos caben en el mismo tablero.
            int maxPeldanos = Mathf.Max(peldanos.Count,
                VdfLadderPrograma.Crear(VdfModoMando.Interruptores).Count);

            const float altoPeldano = 0.050f;
            const float margenSup = 0.060f;
            const float margenInf = 0.030f;
            float alto = margenSup + maxPeldanos * altoPeldano + margenInf;

            var raiz = new GameObject(NombreRaiz);
            raiz.transform.position = posicion;
            raiz.transform.rotation = Quaternion.Euler(0f, rotacionY, 0f);
            raiz.transform.localScale = Vector3.one * Mathf.Max(0.05f, escala);

            var panel = raiz.AddComponent<VdfLadderPanel>();
            panel._peldanos = peldanos;

            // Chapa de fondo.
            Caja(raiz.transform, "Fondo", Vector3.zero,
                 new Vector3(ancho, alto, 0.014f), panel.ColorFondo, 0.15f, false);

            panel._titulo = Texto(raiz.transform, "Titulo",
                new Vector3(0f, alto * 0.5f - 0.026f, -0.009f),
                new Vector2(ancho - 0.04f, 0.028f), 0.017f,
                TextAlignmentOptions.Center, new Color(1f, 1f, 1f));
            panel._titulo.text = "DIAGRAMA LADDER  —  SOLO PARA MIRAR   <size=60%>se ilumina con lo que hace el PLC</size>";

            // Raíles de alimentación, izquierda y derecha, como en cualquier esquema.
            float yPrimero = alto * 0.5f - margenSup - altoPeldano * 0.5f;
            float yUltimo = yPrimero - (maxPeldanos - 1) * altoPeldano;
            float altoRail = (yPrimero - yUltimo) + altoPeldano;
            float xRail = ancho * 0.5f - 0.030f;

            Caja(raiz.transform, "RailIzq", new Vector3(-xRail, (yPrimero + yUltimo) * 0.5f, -0.009f),
                 new Vector3(0.0045f, altoRail, 0.003f), panel.ColorVivo, 0.3f, true);
            Caja(raiz.transform, "RailDer", new Vector3(xRail, (yPrimero + yUltimo) * 0.5f, -0.009f),
                 new Vector3(0.0045f, altoRail, 0.003f), panel.ColorMuerto, 0.3f, false);

            panel._vistas = new List<VdfLadderPeldanoVista>();
            for (int i = 0; i < peldanos.Count; i++)
            {
                var vista = VdfLadderPeldanoVista.Construir(raiz.transform, peldanos[i], i,
                    new Vector3(0f, yPrimero - i * altoPeldano, -0.010f),
                    ancho - 0.075f, altoPeldano, panel);
                panel._vistas.Add(vista);
            }

            // La altura va en unidades LOCALES: la raíz está escalada, así que si se le
            // pasara la altura de mundo las patas saldrían un 55 % más largas y el tablero
            // quedaría enterrado en el suelo.
            if (conPie) ConstruirPie(raiz.transform, posicion.y / Mathf.Max(0.05f, escala),
                                     ancho, alto, panel);

            panel.Refrescar();
            return panel;
        }

        /// <summary>
        /// Patas y base del tablero. La altura se calcula desde el suelo, así que el pie
        /// se alarga o se acorta solo si se cambia la altura a la que va el panel.
        /// </summary>
        private static void ConstruirPie(Transform raiz, float alturaMundo, float ancho,
                                         float alto, VdfLadderPanel panel)
        {
            var colorPie = new Color(0.22f, 0.24f, 0.27f);

            float yBasePanel = -alto * 0.5f;
            float alturaPata = alturaMundo + yBasePanel;   // desde el suelo hasta el panel
            if (alturaPata <= 0.02f) return;

            float dx = ancho * 0.5f - 0.07f;
            for (int i = -1; i <= 1; i += 2)
            {
                Caja(raiz, "Pata_" + i,
                     new Vector3(i * dx, yBasePanel - alturaPata * 0.5f, 0f),
                     new Vector3(0.035f, alturaPata, 0.035f), colorPie, 0.2f, false);
            }

            Caja(raiz, "Base",
                 new Vector3(0f, yBasePanel - alturaPata + 0.012f, 0.02f),
                 new Vector3(ancho * 0.7f, 0.024f, 0.26f), colorPie, 0.2f, false);
        }

        // =====================================================================================
        // Utilidades de dibujo, compartidas con la vista de cada peldaño
        // =====================================================================================

        internal static GameObject Caja(Transform padre, string nombre, Vector3 pos,
                                        Vector3 tamano, Color color, float suavidad,
                                        bool emisivo)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            go.transform.localScale = tamano;

            // Sin collider: el diagrama no se toca.
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = VdfPanelBuilder.ObtenerMaterial(color, suavidad, emisivo);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        internal static TextMeshPro Texto(Transform padre, string nombre, Vector3 pos,
                                          Vector2 caja, float tamano,
                                          TextAlignmentOptions alineacion, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.color = color;
            tmp.alignment = alineacion;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = false;
            // Mismo factor que el resto de paneles: 0,1 unidades de mundo por punto.
            tmp.fontSize = tamano * 10f;
            tmp.rectTransform.sizeDelta = caja;
            return tmp;
        }

        /// <summary>Material de un tramo según lleve corriente o no.</summary>
        internal Material MaterialTramo(bool vivo)
        {
            return vivo
                ? VdfPanelBuilder.ObtenerMaterial(ColorVivo, 0.3f, true)
                : VdfPanelBuilder.ObtenerMaterial(ColorMuerto, 0.3f);
        }
    }
}
