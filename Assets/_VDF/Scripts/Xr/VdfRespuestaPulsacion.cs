using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    public static class VdfRespuestaPulsacion
    {
        public const float AmplitudVibracion = 0.35f;
        public const float DuracionVibracion = 0.05f;

        private static AudioClip _clic;
        private static AudioSource _fuente;

        public static void Dar(Vector3 donde, Object interactor)
        {
            if (!Application.isPlaying) return;
            Sonar(donde);
            Vibrar(interactor);
        }

        public static void Vibrar(Object interactor)
        {
            var mando = interactor as XRBaseInputInteractor;
            if (mando != null) mando.SendHapticImpulse(AmplitudVibracion, DuracionVibracion);
        }

        public static void Sonar(Vector3 donde)
        {
            if (_fuente == null)
            {
                var go = new GameObject("VDF_SonidoPulsacion");
                Object.DontDestroyOnLoad(go);
                _fuente = go.AddComponent<AudioSource>();
                _fuente.playOnAwake = false;
                _fuente.spatialBlend = 1f;
                _fuente.minDistance = 0.3f;
                _fuente.maxDistance = 5f;
                _fuente.volume = 0.7f;
            }
            if (_clic == null) _clic = CrearClic();

            _fuente.transform.position = donde;
            _fuente.PlayOneShot(_clic);
        }

        /// <summary>Un clic seco: 30 ms de una onda de 2,2 kHz que se apaga muy rápido.</summary>
        private static AudioClip CrearClic()
        {
            const int frecuenciaMuestreo = 44100;
            int muestras = frecuenciaMuestreo * 30 / 1000;
            var datos = new float[muestras];
            for (int i = 0; i < muestras; i++)
            {
                float t = i / (float)frecuenciaMuestreo;
                float envolvente = Mathf.Exp(-t * 180f);
                datos[i] = Mathf.Sin(2f * Mathf.PI * 2200f * t) * envolvente * 0.8f;
            }
            var clip = AudioClip.Create("VDF_Clic", muestras, 1, frecuenciaMuestreo, false);
            clip.SetData(datos, 0);
            return clip;
        }
    }
}
