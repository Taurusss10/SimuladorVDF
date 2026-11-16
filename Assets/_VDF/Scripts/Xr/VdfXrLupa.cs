using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    /// <summary>
    /// Lupa en la mano izquierda para leer rótulos, bornes y la pantalla del V1000 sin acercarse.
    ///
    /// <b>X</b> la muestra o la oculta y <b>Y</b> cambia el aumento entre 2×, 3× y 4×. Es un
    /// círculo de 9 cm delante del mando que enseña ampliado lo que hay detrás de él, como una
    /// lupa de verdad: una cámara situada en los ojos del estudiante mira a través del centro de
    /// la lente con un campo de visión igual al tamaño aparente de la lente dividido por el
    /// aumento, y dibuja sobre una textura que se pinta en el círculo. Así lo que se ve dentro
    /// coincide con lo que rodea a la lupa, solo que más grande.
    ///
    /// En realidad virtual no se puede ampliar toda la vista (cambiar el campo de visión del
    /// visor marea y lo controla el propio visor), por eso se usa una lupa, como en otras
    /// aplicaciones de VR. La cámara solo dibuja mientras la lupa está a la vista, a 512 × 512
    /// y sin sombras ni postproceso, para no cargar a la Quest.
    /// </summary>
    public class VdfXrLupa : MonoBehaviour
    {
        public const string NombreObjeto = "VDF_Lupa";
        public const float Diametro = 0.09f;
        public const int Resolucion = 512;
        public static readonly float[] Aumentos = { 2f, 3f, 4f };

        private InputAction _x, _y;
        private NearFarInteractor _izquierdo;
        private GameObject _lente;
        private Camera _camara;
        private RenderTexture _textura;
        private TextMeshPro _rotulo;
        private int _indiceAumento;

        public bool Visible => _lente != null && _lente.activeSelf;
        public float Aumento => Aumentos[_indiceAumento];

        /// <summary>Crea la lupa una sola vez en la escena de VR. Sin rig de VR no hace nada.</summary>
        public static VdfXrLupa Asegurar()
        {
            if (Object.FindAnyObjectByType<NearFarInteractor>(FindObjectsInactive.Include) == null) return null;
            var existente = Object.FindAnyObjectByType<VdfXrLupa>();
            if (existente != null) return existente;
            return new GameObject(NombreObjeto).AddComponent<VdfXrLupa>();
        }

        private void OnEnable()
        {
            _x = new InputAction("Lupa", InputActionType.Button);
            _x.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            _y = new InputAction("Aumento", InputActionType.Button);
            _y.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
            _x.Enable(); _y.Enable();
        }

        private void OnDisable()
        {
            _x?.Dispose(); _y?.Dispose();
            _x = _y = null;
        }

        private void OnDestroy()
        {
            if (_textura != null) { _textura.Release(); Destroy(_textura); }
        }

        private void Update()
        {
            if (_x == null || _y == null) return;
            // Flanco del estado, comparando con el fotograma anterior (ver VdfXrAtajosV1000).
            bool x = _x.IsPressed(), y = _y.IsPressed();
            if (x && !_xAntes) Alternar();
            if (y && !_yAntes) SiguienteAumento();
            _xAntes = x; _yAntes = y;
        }

        private bool _xAntes, _yAntes;

        private void LateUpdate()
        {
            if (!Visible) return;
            if (_izquierdo == null) _izquierdo = VdfXrAtajosV1000.BuscarInteractor(InteractorHandedness.Left);
            if (_izquierdo == null) return;
            // Cada fotograma y no solo al abrirla: el mando puede encenderse después.
            _izquierdo.enableFarCasting = false;

            // La lente va 12 cm por delante del mando y un poco por encima.
            Transform mano = _izquierdo.transform;
            Vector3 centro = mano.position + mano.forward * 0.12f + mano.up * 0.03f;

            // Siempre de cara a los ojos, y la cámara en los ojos mirando a través de ella.
            Camera ojos = Camera.main;
            if (ojos == null) { _lente.transform.SetPositionAndRotation(centro, mano.rotation); return; }
            Vector3 ojo = ojos.transform.position;
            Vector3 hacia = centro - ojo;
            float distancia = Mathf.Max(hacia.magnitude, 0.05f);
            var mirada = Quaternion.LookRotation(hacia / distancia, ojos.transform.up);
            _lente.transform.SetPositionAndRotation(centro, mirada);
            _camara.transform.SetPositionAndRotation(ojo, mirada);

            // Campo = tamaño aparente de la lente / aumento. El plano cercano queda justo detrás
            // de la lente, para que la cámara no vea la mano ni la propia lupa.
            float aparente = 2f * Mathf.Atan(Diametro * 0.5f / distancia) * Mathf.Rad2Deg;
            _camara.fieldOfView = aparente / Aumento;
            _camara.nearClipPlane = distancia + 0.01f;
        }

        /// <summary>Campo de visión que tendría la cámara de la lupa con la lente a
        /// <paramref name="distancia"/> de los ojos y el aumento dado. Para las pruebas.</summary>
        public static float CampoDeVision(float distancia, float aumento)
        {
            return 2f * Mathf.Atan(Diametro * 0.5f / distancia) * Mathf.Rad2Deg / aumento;
        }

        /// <summary>Muestra u oculta la lupa (botón X).</summary>
        public void Alternar()
        {
            if (_lente == null) Construir();
            bool mostrar = !_lente.activeSelf;
            _lente.SetActive(mostrar);
            _camara.enabled = mostrar;

            // Con la lupa en la mano, el rayo largo del mando izquierdo pasaría a través de ella:
            // sacaría la etiqueta flotante encima de la lente y podría pulsar algo sin querer.
            if (_izquierdo == null) _izquierdo = VdfXrAtajosV1000.BuscarInteractor(InteractorHandedness.Left);
            if (_izquierdo != null) _izquierdo.enableFarCasting = !mostrar;
        }

        /// <summary>Pasa al aumento siguiente: 2×, 3×, 4× y vuelta a 2× (botón Y).</summary>
        public void SiguienteAumento()
        {
            _indiceAumento = (_indiceAumento + 1) % Aumentos.Length;
            AplicarAumento();
        }

        private void AplicarAumento()
        {
            if (_rotulo != null) _rotulo.text = Aumento.ToString("0") + "×";
        }

        private void Construir()
        {
            _lente = new GameObject("Lente");
            _lente.transform.SetParent(transform, false);

            _textura = new RenderTexture(Resolucion, Resolucion, 16) { name = "VDF_Lupa" };
            _textura.Create();

            // Cámara de la lupa: la coloca LateUpdate en los ojos, mirando a través de la lente.
            var goCamara = new GameObject("Camara");
            goCamara.transform.SetParent(_lente.transform, false);
            _camara = goCamara.AddComponent<Camera>();
            _camara.stereoTargetEye = StereoTargetEyeMask.None;
            _camara.targetTexture = _textura;
            _camara.nearClipPlane = 0.3f;
            _camara.fieldOfView = CampoDeVision(0.4f, Aumento);
            _camara.farClipPlane = 20f;
            _camara.allowMSAA = false;
            var datos = _camara.GetUniversalAdditionalCameraData();
            datos.renderShadows = false;
            datos.renderPostProcessing = false;

            // El círculo con la imagen, mirando al estudiante.
            var cristal = new GameObject("Cristal");
            cristal.transform.SetParent(_lente.transform, false);
            cristal.AddComponent<MeshFilter>().sharedMesh = Disco(Diametro * 0.5f, 40);
            var material = MaterialSinLuz();
            material.mainTexture = _textura;
            cristal.AddComponent<MeshRenderer>().sharedMaterial = material;

            // Aro oscuro alrededor, para que se lea como una lupa y no como un hueco.
            var aro = new GameObject("Aro");
            aro.transform.SetParent(_lente.transform, false);
            aro.transform.localPosition = new Vector3(0f, 0f, 0.001f);
            aro.AddComponent<MeshFilter>().sharedMesh = Disco(Diametro * 0.5f + 0.006f, 40);
            var negro = MaterialSinLuz();
            negro.color = new Color(0.08f, 0.08f, 0.09f);
            aro.AddComponent<MeshRenderer>().sharedMaterial = negro;

            // Aumento escrito debajo de la lente.
            var goRotulo = new GameObject("Aumento");
            goRotulo.transform.SetParent(_lente.transform, false);
            goRotulo.transform.localPosition = new Vector3(0f, -Diametro * 0.5f - 0.014f, -0.001f);
            goRotulo.transform.localRotation = Quaternion.identity;
            _rotulo = goRotulo.AddComponent<TextMeshPro>();
            _rotulo.alignment = TextAlignmentOptions.Center;
            _rotulo.fontSize = 0.16f;
            _rotulo.rectTransform.sizeDelta = new Vector2(0.06f, 0.02f);
            _rotulo.color = Color.white;

            _lente.SetActive(false);
            _camara.enabled = false;
            AplicarAumento();
        }

        /// <summary>
        /// Material sin iluminación y de dos caras. En el instalable solo existen los shaders
        /// que usa alguna escena, así que se cae a «Sprites/Default», que siempre se incluye.
        /// </summary>
        private static Material MaterialSinLuz()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            return m;
        }

        /// <summary>Disco en el plano XY con UV de 0 a 1, para pintar la textura entera.</summary>
        private static Mesh Disco(float radio, int lados)
        {
            var v = new Vector3[lados + 1];
            var uv = new Vector2[lados + 1];
            var t = new int[lados * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < lados; i++)
            {
                float a = i * Mathf.PI * 2f / lados;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i + 1] = new Vector3(c * radio, s * radio, 0f);
                uv[i + 1] = new Vector2(0.5f + c * 0.5f, 0.5f + s * 0.5f);
                t[i * 3] = 0; t[i * 3 + 1] = (i + 1) % lados + 1; t[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { name = "VDF_Disco", vertices = v, uv = uv, triangles = t };
            m.RecalculateNormals();
            return m;
        }
    }
}
