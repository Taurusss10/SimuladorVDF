
using TMPro;
using UnityEngine;

namespace VDF
{
    public class VdfEtiquetaFlotante : MonoBehaviour
    {
        public const float TamanoFuente = 0.2f;
        public const float Elevacion = 0.035f;

        private static VdfEtiquetaFlotante _instancia;

        private TextMeshPro _texto;
        private Object _dueno;
        private Vector3 _ancla;
        private bool _debajo;
        private string _plano;
        private float _medioAlto;

        /// <summary>Hueco entre el ancla y el borde de arriba de la etiqueta, colgada por debajo.</summary>
        public const float Holgura = 0.008f;

        /// <summary>Lo que sobresale la placa alrededor del texto.</summary>
        public const float MargenPlaca = 0.006f;

        public const float Adelanto = 0.06f;

        /// <summary>Cuánto se acerca al ojo, sin cambiar cómo se ve. Ver Colocar.</summary>
        public const float AcercarAlOjo = 0.18f;

        private Transform _placa;
        private static Material _materialPlaca;
        public static readonly Color ColorPlaca = new Color(0.04f, 0.05f, 0.07f, 0.72f);

        /// <param name="debajo">Colgarla por debajo de <paramref name="donde"/> en vez de por encima.</param>
        public static void Mostrar(Object dueno, Vector3 donde, string texto, bool debajo = false)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(texto)) return;
            var e = Instancia();
            e._dueno = dueno;
            e._ancla = donde;
            e._debajo = debajo;
            e._plano = texto;
            e._texto.text = texto;
            // Activa ANTES de medir: desactivada, TextMeshPro no remaqueta y devolvía las
            // medidas del texto anterior (la placa quedaba más pequeña que el texto).
            e.gameObject.SetActive(true);
            // Medio alto del texto ya maquetado: colgada por debajo, el borde de arriba tiene
            // que quedar bajo el ancla tenga las líneas que tenga (LO/RE tiene tres).
            e._texto.ForceMeshUpdate();
            // La malla real, no textBounds: con varias líneas de distinto tamaño (LO/RE)
            // textBounds quedaba corrido media línea y la placa no cubría la última.
            var b = e._texto.bounds;
            // A escala 1: la que se le da en Colocar es solo para acercarla al ojo.
            e._medioAlto = b.extents.y + MargenPlaca;
            e._placa.localPosition = new Vector3(b.center.x, b.center.y, 0.002f);
            e._placa.localScale = new Vector3(b.size.x + 2f * MargenPlaca, b.size.y + 2f * MargenPlaca, 1f);
            e.Colocar();
        }

        /// <summary>Lo oculta, pero solo si lo está mostrando ese mismo dueño.</summary>
        public static void Ocultar(Object dueno)
        {
            if (_instancia == null || _instancia._dueno != dueno) return;
            _instancia._dueno = null;
            _instancia.gameObject.SetActive(false);
        }

        public static bool Visible(out string texto)
        {
            texto = _instancia != null && _instancia.gameObject.activeSelf ? _instancia._plano : null;
            return texto != null;
        }

        /// <summary>Lo que ocupa en el mundo, para comprobar qué tapa. Para los tests.</summary>
        public static Bounds Limites()
        {
            if (_instancia == null) return new Bounds();
            _instancia._texto.ForceMeshUpdate();
            return _instancia._texto.bounds.size == Vector3.zero
                ? new Bounds(_instancia.transform.position, Vector3.zero)
                : new Bounds(_instancia.transform.TransformPoint(_instancia._texto.bounds.center),
                             Vector3.Scale(_instancia._texto.bounds.size, _instancia.transform.lossyScale));
        }

        /// <summary>Lo que mide en grados desde el puesto más cercano. Para los tests.</summary>
        public static float AlturaAngular()
        {
            return _instancia != null ? VdfAuditoriaXr.AlturaAngular(_instancia._texto) : 0f;
        }

        private static VdfEtiquetaFlotante Instancia()
        {
            if (_instancia != null) return _instancia;

            var go = new GameObject("VDF_EtiquetaFlotante");
            _instancia = go.AddComponent<VdfEtiquetaFlotante>();
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = TamanoFuente;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.rectTransform.sizeDelta = new Vector2(0.4f, 0.05f);
            _instancia._texto = tmp;

            var placa = GameObject.CreatePrimitive(PrimitiveType.Quad);
            placa.name = "Placa";
            Object.Destroy(placa.GetComponent<Collider>());
            placa.transform.SetParent(go.transform, false);
            var r = placa.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (_materialPlaca == null)
            {
                _materialPlaca = new Material(Shader.Find("Sprites/Default"));
                _materialPlaca.color = ColorPlaca;
                // Antes que el texto, que es transparente también: primero la placa, luego la letra.
                _materialPlaca.renderQueue = 2990;
            }
            r.sharedMaterial = _materialPlaca;
            _instancia._placa = placa.transform;
            return _instancia;
        }

        private void LateUpdate()
        {
            Colocar();
        }

        private void Colocar()
        {
            var cam = Camera.main;
            Vector3 haciaOjos = cam != null ? (cam.transform.position - _ancla).normalized : Vector3.back;
            float alto = _debajo ? -(Holgura + _medioAlto) : Elevacion;
            // Colgada por debajo, se adelanta solo en horizontal: como el alumno mira desde
            // arriba, adelantarla hacia los ojos también la subía, y volvía a tapar el teclado.
            if (_debajo)
            {
                haciaOjos.y = 0f;
                haciaOjos = haciaOjos.sqrMagnitude > 1e-6f ? haciaOjos.normalized : Vector3.back;
            }
            Vector3 donde = _ancla + Vector3.up * alto + haciaOjos * Adelanto;
            float escala = 1f;

            if (cam != null)
            {
                Vector3 ojo = cam.transform.position;
                Vector3 rayo = donde - ojo;
                float distancia = rayo.magnitude;
                if (distancia > 1e-3f)
                {
                    escala = Mathf.Clamp((distancia - AcercarAlOjo) / distancia, 0.4f, 1f);
                    donde = ojo + rayo * escala;
                }
            }

            transform.position = donde;
            transform.localScale = Vector3.one * escala;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }

        private void OnDestroy()
        {
            if (_instancia == this) _instancia = null;
        }

        // =====================================================================================
        // Nombres
        // =====================================================================================

        /// <summary>Cómo se llama un borne para alguien que no se sabe los códigos.</summary>
        public static string NombreDeBorne(VdfTerminal t)
        {
            if (t == null) return null;
            return NombreDeId(t.Id);
        }

        /// <summary>Igual, a partir del id del borne o del nombre del pulsador.</summary>
        public static string NombreDeId(string id)
        {
            id = id ?? "";
            if (id.StartsWith("Pulsador") || id.StartsWith("Interruptor_")) return NombreDePulsador(id);

            if (id == "BRK-L1") return "F1  (breaker)";
            if (id == "BRK-L3") return "F2  (breaker)";
            if (id == "BRK-L2") return "Breaker, borne del centro";
            if (id.StartsWith("BRK-")) return "Breaker " + id.Substring(4);
            if (id.StartsWith("PLC-")) return "PLC  " + id.Substring(4);
            if (id.StartsWith("MOT-")) return "Motor  " + id.Substring(4);
            if (id.EndsWith("_MOT")) return id.Substring(0, id.Length - 4) + "  (salida a motor)";
            return id;
        }

        public static string NombreDeTecla(VfdKey tecla)
        {
            string nombre = VfdKeypadView.EtiquetaDe(tecla);
            string uso;
            switch (tecla)
            {
                case VfdKey.Esc: uso = "Volver atrás, sin guardar"; break;
                case VfdKey.Enter: uso = "Entrar en un menú, editar o guardar"; break;
                case VfdKey.Arriba: uso = "Siguiente menú, parámetro o dígito +1"; break;
                case VfdKey.Abajo: uso = "Anterior menú, parámetro o dígito -1"; break;
                case VfdKey.Reset: uso = "Pasa al dígito siguiente. Borra una falla"; break;
                case VfdKey.Run: uso = "Marcha desde el teclado. Aquí no se usa"; break;
                case VfdKey.Stop: uso = "Para el motor desde el teclado"; break;
                case VfdKey.LoRe: uso = "Local o remoto. No la toques: manda el PLC"; break;
                default: uso = null; break;
            }
            return uso == null ? nombre : nombre + (char)10 + "<size=80%>" + uso + "</size>";
        }

        /// <summary>Cómo se llama un pulsador del tablero.</summary>
        public static string NombreDePulsador(string nombreObjeto)
        {
            if (string.IsNullOrEmpty(nombreObjeto)) return null;
            if (nombreObjeto.Contains("_PE")) return "PARADA DE EMERGENCIA";
            if (nombreObjeto == VdfInterruptoresBuilder.NombreMarcha) return "S4  ·  entrada I0.0 del PLC";
            if (nombreObjeto == VdfInterruptoresBuilder.NombreS5) return "S5  ·  entrada I0.1 del PLC";
            if (nombreObjeto == VdfInterruptoresBuilder.NombreS6) return "S6  ·  entrada I0.2 del PLC";
            int i = nombreObjeto.LastIndexOf('_');
            string nombre = i >= 0 ? nombreObjeto.Substring(i + 1) : nombreObjeto;

            if (VdfMando.Laboratorio && nombreObjeto.StartsWith("Pulsador"))
                nombre += (char)10 + "<size=80%>No se usa en esta práctica: manda S4, encima del PLC</size>";
            return nombre;
        }
    }
}
