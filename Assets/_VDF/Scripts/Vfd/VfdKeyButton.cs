// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Unity crea un unico MonoScript por archivo, con el nombre del archivo; cualquier
// MonoBehaviour cuya clase no coincida NO obtiene MonoScript y, por tanto, no se puede
// deserializar. Funciona dentro de la sesion (el tipo existe en memoria) y se rompe al
// reiniciar el editor, dejando "The referenced script is missing" en la escena.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y, probablemente, un crash.

using UnityEngine;

namespace VDF
{
    /// <summary>Marca un objeto de la escena como una tecla concreta del operador digital.</summary>
    public class VfdKeyButton : MonoBehaviour
    {
        public VfdKey Tecla;

        [Tooltip("Parte que se hunde al pulsar.")]
        public Transform Capuchon;

        private Vector3 _reposo;
        private float _tiempoHundida;

        private void Awake()
        {
            if (Capuchon != null) _reposo = Capuchon.localPosition;
        }

        /// <summary>Animación corta de pulsación, para que se note que se registró el toque.</summary>
        public void Animar()
        {
            _tiempoHundida = 0.12f;
        }

        private void Update()
        {
            if (Capuchon == null) return;

            if (_tiempoHundida > 0f)
            {
                _tiempoHundida -= Time.deltaTime;
                Capuchon.localPosition = _reposo + new Vector3(0f, 0f, 0.0022f);
            }
            else
            {
                Capuchon.localPosition = _reposo;
            }
        }
    }
}
