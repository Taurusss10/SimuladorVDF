
using UnityEngine;

namespace VDF
{
    public class VdfPistaObjetivo : MonoBehaviour
    {
        [Tooltip("Segundos atascado en el mismo paso antes de encender la pista.")]
        public float SegundosHastaLaPista = 0f;

        [Tooltip("Parpadeos por segundo cuando la pista esta encendida.")]
        public float Parpadeos = 1.1f;

        public VdfPracticaRunner Runner;
        public VdfJumperController Cableado;

        private int _pasoVigilado = -99;
        private float _desdeCuando;
        private readonly System.Collections.Generic.List<VdfTerminal> _objetivos =
            new System.Collections.Generic.List<VdfTerminal>();
        private bool _encendido;

        private readonly System.Collections.Generic.List<LineRenderer> _aros =
            new System.Collections.Generic.List<LineRenderer>();

        public static readonly Color ColorAro = new Color(1f, 0.15f, 0.85f);
        public const float RadioAro = 0.017f;
        public const float GrosorAro = 0.003f;
        private const int PuntosAro = 28;
        private static Material _materialAro;

        /// <summary>Cuántos aros se ven ahora mismo. Para los tests.</summary>
        public int ArosVisibles
        {
            get
            {
                int n = 0;
                foreach (var a in _aros) if (a != null && a.gameObject.activeSelf) n++;
                return n;
            }
        }

        private void Awake()
        {
            if (Runner == null) Runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            if (Cableado == null) Cableado = Object.FindAnyObjectByType<VdfJumperController>();
        }

        private void Update()
        {
            if (Runner == null || Runner.Practica == null) { Apagar(); return; }

            int paso = Runner.PasoActual();
            if (paso != _pasoVigilado)
            {
                // Paso nuevo: a empezar de cero, y lo que estuviera parpadeando se apaga.
                Apagar();
                _pasoVigilado = paso;
                _desdeCuando = Time.time;
                _objetivos.Clear();
                if (paso >= 0) BuscarObjetivos(Runner.Practica.Paso(paso));
                return;
            }

            if (_objetivos.Count == 0) return;
            if (Time.time - _desdeCuando < SegundosHastaLaPista) return;

            // Parpadeo: medio periodo encendido, medio apagado.
            bool debe = Mathf.Repeat((Time.time - _desdeCuando) * Parpadeos, 1f) < 0.5f;
            if (debe == _encendido) return;

            _encendido = debe;
            // Si el borne ya esta resaltado por otra razon -tiene un cable puesto- no se
            // toca: ese resaltado significa otra cosa y apagarlo confundiria mas.
            for (int i = 0; i < _objetivos.Count; i++)
                if (!_objetivos[i].TieneCableEnchufado) _objetivos[i].Resaltar(debe);
            MostrarAros(debe);
        }

        private void LateUpdate()
        {
            // Los aros miran siempre al alumno, para que se vean redondos desde cualquier puesto.
            var cam = Camera.main;
            if (cam == null) return;
            for (int i = 0; i < _aros.Count && i < _objetivos.Count; i++)
            {
                var aro = _aros[i];
                if (aro == null || !aro.gameObject.activeSelf || _objetivos[i] == null) continue;
                Vector3 borne = _objetivos[i].transform.position;
                Vector3 haciaOjos = (cam.transform.position - borne).normalized;
                aro.transform.position = borne + haciaOjos * 0.006f;
                aro.transform.rotation = Quaternion.LookRotation(-haciaOjos, Vector3.up);
            }
        }

        private void MostrarAros(bool si)
        {
            while (_aros.Count < _objetivos.Count) _aros.Add(CrearAro());
            for (int i = 0; i < _aros.Count; i++)
                if (_aros[i] != null)
                    _aros[i].gameObject.SetActive(si && i < _objetivos.Count && !_objetivos[i].TieneCableEnchufado);
        }

        private LineRenderer CrearAro()
        {
            var go = new GameObject("VDF_AroPista");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = PuntosAro;
            for (int i = 0; i < PuntosAro; i++)
            {
                float a = i * Mathf.PI * 2f / PuntosAro;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * RadioAro, Mathf.Sin(a) * RadioAro, 0f));
            }
            lr.startWidth = lr.endWidth = GrosorAro;
            lr.startColor = lr.endColor = ColorAro;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (_materialAro == null)
            {
                // Sin luz: el magenta se ve igual de vivo en la sombra del tablero. Un único
                // material para todos los aros.
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _materialAro = new Material(shader);
                _materialAro.SetColor("_BaseColor", ColorAro);
                _materialAro.color = ColorAro;
            }
            lr.sharedMaterial = _materialAro;
            go.SetActive(false);
            return lr;
        }

        /// <summary>
        /// De que bornes habla el paso, si es que habla de alguno.
        ///
        /// Un paso de union trae los dos extremos separados por «|» («BRK-L1|R/L1»).
        /// Buscarlo tal cual no encontraba nada, asi que en la practica de cableado, que es
        /// donde mas se atasca la gente, la pista no salia nunca. Se resaltan los dos.
        /// </summary>
        private void BuscarObjetivos(VdfPasoPractica paso)
        {
            if (paso == null || Cableado == null || string.IsNullOrEmpty(paso.Objetivo)) return;

            switch (paso.Condicion)
            {
                case VdfCondicion.ComponenteIdentificado:
                case VdfCondicion.BornesUnidos:
                    foreach (var id in paso.Objetivo.Split('|'))
                    {
                        var t = Cableado.BuscarBorne(id.Trim());
                        if (t != null) _objetivos.Add(t);
                    }
                    break;
                case VdfCondicion.GrupoIdentificado:
                    // Un grupo («Indicadores de velocidad») se señala tocando cualquiera de
                    // sus bornes: se encienden todos.
                    foreach (var t in Object.FindObjectsByType<VdfTerminal>())
                        if (t.Grupo == paso.Objetivo) _objetivos.Add(t);
                    break;
                default:
                    // Los pasos de parametro, de operacion o de diagnostico no se senalan:
                    // ahi el objetivo no es un sitio del tablero sino una accion.
                    break;
            }
        }

        private void Apagar()
        {
            MostrarAros(false);
            if (_encendido)
                for (int i = 0; i < _objetivos.Count; i++)
                    if (_objetivos[i] != null && !_objetivos[i].TieneCableEnchufado)
                        _objetivos[i].Resaltar(false);
            _encendido = false;
        }

        private void OnDisable() { Apagar(); }
    }
}
