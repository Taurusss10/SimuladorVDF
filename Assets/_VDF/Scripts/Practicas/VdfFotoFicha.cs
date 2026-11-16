
using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    public class VdfFotoFicha : MonoBehaviour
    {
        public const int Resolucion = 384;

        /// <summary>Lo mínimo que abarca la foto, en metros: una pieza y su entorno.</summary>
        public const float EncuadreMinimo = 0.16f;

        public VdfPanelGuia Guia;
        public Renderer Pantalla;

        private Camera _camara;
        private RenderTexture _textura;
        private Material _material;
        private VdfPasoPractica _mostrada;
        private bool _primera = true;

        private void Update()
        {
            if (Guia == null || Pantalla == null) return;

            var runner = Guia.Runner;
            var ficha = runner != null
                ? VdfPanelGuia.UltimaFicha(runner.Practica, runner.PasoActual())
                : null;

            if (!_primera && ficha == _mostrada) return;
            _primera = false;
            _mostrada = ficha;

            Bounds encuadre;
            List<VdfTerminal> marcados;
            if (ficha == null || !Encuadrar(ficha, out encuadre, out marcados))
            {
                Pantalla.gameObject.SetActive(false);
                return;
            }

            // La pieza sale RESALTADA en la foto: así se ve cuál es entre sus vecinas.
            foreach (var t in marcados) t.Resaltar(true);
            Fotografiar(encuadre);
            foreach (var t in marcados) if (!t.TieneCableEnchufado) t.Resaltar(false);
            Pantalla.gameObject.SetActive(true);
        }

        /// <summary>Qué hay que sacar en la foto para este paso.</summary>
        public static bool Encuadrar(VdfPasoPractica paso, out Bounds encuadre)
        {
            List<VdfTerminal> marcados;
            return Encuadrar(paso, out encuadre, out marcados);
        }

        public static bool Encuadrar(VdfPasoPractica paso, out Bounds encuadre,
                                     out List<VdfTerminal> marcados)
        {
            encuadre = new Bounds();
            marcados = new List<VdfTerminal>();
            var piezas = new List<Transform>();

            if (paso != null && !string.IsNullOrEmpty(paso.Objetivo))
            {
                var bornes = Object.FindObjectsByType<VdfTerminal>();
                switch (paso.Condicion)
                {
                    case VdfCondicion.ComponenteIdentificado:
                        foreach (var id in paso.Objetivo.Split('|'))
                            foreach (var t in bornes)
                                if (t.Id == id.Trim()) { piezas.Add(t.transform); marcados.Add(t); }
                        break;

                    case VdfCondicion.GrupoIdentificado:
                        foreach (var t in bornes)
                            if (t.Grupo == paso.Objetivo) { piezas.Add(t.transform); marcados.Add(t); }
                        // Los pulsadores no son bornes, pero son lo que hay que ver.
                        if (paso.Objetivo == "Mando")
                            foreach (var p in Object.FindObjectsByType<VdfPulsadorTablero>())
                                if (!p.EsInterruptor) piezas.Add(p.transform);
                        if (paso.Objetivo == VdfPracticasDefaults.GrupoMandoLaboratorio)
                            foreach (var p in Object.FindObjectsByType<VdfPulsadorTablero>())
                                if (p.EsInterruptor || p.Accion == VdfAccionPulsador.Emergencia) piezas.Add(p.transform);
                        break;
                }
            }

            if (piezas.Count == 0) return false;

            encuadre = new Bounds(piezas[0].position, Vector3.zero);
            foreach (var p in piezas) encuadre.Encapsulate(p.position);
            return true;
        }

        private void Fotografiar(Bounds encuadre)
        {
            if (_textura == null)
            {
                _textura = new RenderTexture(Resolucion, Resolucion, 16);
                _textura.name = "FotoFicha";
            }

            if (_camara == null)
            {
                var go = new GameObject("CamaraFotoFicha");
                go.transform.SetParent(transform, false);
                _camara = go.AddComponent<Camera>();
                _camara.enabled = false;
                _camara.orthographic = true;
                _camara.clearFlags = CameraClearFlags.SolidColor;
                _camara.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
                _camara.nearClipPlane = 0.01f;
                _camara.farClipPlane = 2f;
                _camara.targetTexture = _textura;
            }

            if (_material == null)
            {
                _material = new Material(Pantalla.sharedMaterial);
                _material.SetTexture("_BaseMap", _textura);
                _material.SetTexture("_MainTex", _textura);
                _material.SetTexture("_EmissionMap", _textura);
                _material.SetColor("_BaseColor", Color.white);
                _material.SetColor("_EmissionColor", Color.white * 0.55f);
                _material.EnableKeyword("_EMISSION");
                Pantalla.material = _material;
            }

            // De frente al tablero, que mira hacia -Z: es como lo ve el alumno de pie.
            float lado = Mathf.Max(EncuadreMinimo, Mathf.Max(encuadre.size.x, encuadre.size.y) + 0.05f);
            _camara.orthographicSize = lado * 0.5f;
            _camara.transform.position = encuadre.center + Vector3.back * 0.6f;
            _camara.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            _camara.Render();
        }

        private void OnDestroy()
        {
            if (_textura != null) _textura.Release();
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        /// <summary>
        /// Cuelga el marco de la foto del borde derecho del atril, a la altura de la ficha.
        /// Va FUERA del panel: dentro le quitaba ancho al texto, que ya va justo.
        /// </summary>
        public static VdfFotoFicha Construir(VdfPanelGuia guia, float anchoPanel, float altoPanel)
        {
            float lado = altoPanel * 0.50f;

            var marco = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marco.name = "MarcoFotoFicha";
            marco.transform.SetParent(guia.transform, false);
            marco.transform.localScale = new Vector3(lado + 0.012f, lado + 0.012f, 0.010f);
            marco.transform.localPosition = new Vector3(anchoPanel * 0.5f + lado * 0.5f + 0.012f,
                                                       altoPanel * 0.5f - lado * 0.5f, 0f);
            Quitar(marco.GetComponent<Collider>());
            marco.GetComponent<Renderer>().sharedMaterial =
                VdfPanelBuilder.ObtenerMaterial(new Color(0.45f, 0.85f, 1f), 0.15f);

            var foto = GameObject.CreatePrimitive(PrimitiveType.Quad);
            foto.name = "FotoFicha";
            foto.transform.SetParent(guia.transform, false);
            foto.transform.localScale = new Vector3(lado, lado, 1f);
            foto.transform.localPosition = marco.transform.localPosition + new Vector3(0f, 0f, -0.0062f);
            Quitar(foto.GetComponent<Collider>());
            foto.GetComponent<Renderer>().sharedMaterial =
                VdfPanelBuilder.ObtenerMaterial(Color.white, 0f, true);

            // El marco se enciende y se apaga con la foto.
            marco.transform.SetParent(foto.transform, true);

            var componente = guia.gameObject.AddComponent<VdfFotoFicha>();
            componente.Guia = guia;
            componente.Pantalla = foto.GetComponent<Renderer>();
            foto.SetActive(false);
            return componente;
        }

        private static void Quitar(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
