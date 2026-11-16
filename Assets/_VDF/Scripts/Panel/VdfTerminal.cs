using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Marca un GameObject de la escena como bornera conectable y guarda su identidad.
    ///
    /// Es el puente entre el layout (datos) y la escena: E4 (jumpers de escritorio),
    /// E6 (sockets XR) y la validación de cableado encuentran los bornes buscando este
    /// componente, sin depender de nombres de GameObject ni de jerarquías.
    /// </summary>
    public class VdfTerminal : MonoBehaviour
    {
        [Tooltip("Id único, el mismo que en VdfPanelLayout. Es la clave del grafo de conexiones.")]
        public string Id;

        [Tooltip("Texto serigrafiado junto al borne. Puede repetirse entre bornes distintos.")]
        public string Etiqueta;

        [Tooltip("Agrupación lógica a la que pertenece.")]
        public string Grupo;

        [Tooltip("Punto exacto donde se engancha la punta del cable, en coordenadas locales.")]
        public Vector3 PuntoConexionLocal = Vector3.zero;

        /// <summary>Posición mundial del punto de enganche del jumper.</summary>
        public Vector3 PuntoConexion
        {
            get { return transform.TransformPoint(PuntoConexionLocal); }
        }

        [Header("Resaltado")]
        [Tooltip("Color con el que se marca el borne cuando está seleccionado o apuntado.")]
        public Color ColorResaltado = new Color(1f, 1f, 0.08f);

        private Renderer _renderer;
        private MaterialPropertyBlock _bloque;
        private bool _resaltado;

        /// <summary>
        /// Marca o desmarca el borne visualmente.
        ///
        /// Usa MaterialPropertyBlock en vez de tocar el material: los bornes comparten
        /// material (hay 68 en escena), así que cambiar `material.color` crearía una
        /// instancia por borne y rompería el batching. En Quest eso importa.
        /// </summary>
        public void Resaltar(bool activo)
        {
            if (_resaltado == activo) return;
            _resaltado = activo;

            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer == null) return;

            if (_bloque == null) _bloque = new MaterialPropertyBlock();

            _renderer.GetPropertyBlock(_bloque);
            if (activo)
            {
                _bloque.SetColor("_BaseColor", ColorResaltado);
                _bloque.SetColor("_Color", ColorResaltado);
            }
            else
            {
                _bloque.Clear();
            }
            _renderer.SetPropertyBlock(_bloque);
        }

        public bool EstaResaltado { get { return _resaltado; } }

        /// <summary>
        /// Cuantos conectores hay metidos en este borne ahora mismo.
        ///
        /// Lo lleva <see cref="VdfXrSocket"/>. Sirve para que el resaltado de APUNTAR no
        /// apague el de ESTAR ENCHUFADO: son dos cosas distintas que usan el mismo color, y
        /// sin esto dejar de apuntar un borne ocupado lo apagaba como si estuviera libre.
        /// </summary>
        public int Enchufados
        {
            get { return _enchufados; }
            set
            {
                _enchufados = value < 0 ? 0 : value;
                var propio = GetComponent<Collider>();
                if (propio != null) propio.enabled = _enchufados == 0;
            }
        }
        private int _enchufados;

        /// <summary>¿Hay algun conector metido en este borne?</summary>
        public bool TieneCableEnchufado { get { return Enchufados > 0; } }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Etiqueta) ? Id : Etiqueta + " (" + Id + ")";
        }
    }
}
