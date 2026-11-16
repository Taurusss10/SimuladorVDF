using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    [CreateAssetMenu(fileName = "VdfPractica", menuName = "VDF/Práctica", order = 0)]
    public class VdfPractica : ScriptableObject
    {
        [Tooltip("Número de práctica, del 1 al 5.")]
        [Range(1, 5)] public int Numero = 1;

        public string Titulo = "Práctica";

        [TextArea(2, 5)]
        [Tooltip("De qué va la práctica. Se muestra al elegirla en el menú.")]
        public string Descripcion;

        [Header("Punto de partida")]
        [Tooltip("Aplicar la configuración correcta del variador al empezar.")]
        public bool EmpezarConfigurado;

        [Tooltip("Tender el cableado correcto al empezar.")]
        public bool EmpezarCableado;

        [Tooltip("Si está desmarcado, señalar bornes solo los identifica: no crea cables. " +
                 "Se desmarca en la práctica de reconocimiento.")]
        public bool PermiteCablear = true;

        [Tooltip("El motor no arranca en esta práctica, ni con RUN ni desde el PLC. " +
                 "Se marca en las de reconocimiento y configuración.")]
        public bool BloqueaMarcha;

        [Tooltip("Los pulsadores del tablero solo se señalan: no actúan sobre el PLC. " +
                 "Se marca en la de reconocimiento, donde el módulo está sin cablear.")]
        public bool PulsadoresSoloSenalan;

        [Header("Guion")]
        public List<VdfPasoPractica> Pasos = new List<VdfPasoPractica>();

        [Tooltip("Guion alternativo para cuando se opera con los interruptores en vez de " +
                 "con los pulsadores. Vacío si la práctica es igual en los dos modos.")]
        public List<VdfPasoPractica> PasosConInterruptores = new List<VdfPasoPractica>();

        /// <summary>El guion de pulsadores, guardado al cambiar a interruptores para poder volver.</summary>
        [System.NonSerialized] private List<VdfPasoPractica> _pasosConPulsadores;

        /// <summary>
        /// Elige el guion según el modo de mando. Los dos se conservan: cambiar de modo
        /// cambia qué lista se usa, nunca borra la otra.
        /// </summary>
        public void UsarMando(VdfModoMando modo)
        {
            if (_pasosConPulsadores == null) _pasosConPulsadores = Pasos;

            bool hayAlternativo = PasosConInterruptores != null && PasosConInterruptores.Count > 0;
            Pasos = modo == VdfModoMando.Interruptores && hayAlternativo
                ? PasosConInterruptores
                : _pasosConPulsadores;
        }

        public int TotalPasos { get { return Pasos != null ? Pasos.Count : 0; } }

        /// <summary>
        /// ¿Esta práctica necesita que el módulo tenga una avería provocada?
        ///
        /// Se deduce del guion en vez de ponerse a mano: si hay un paso que pide diagnosticar
        /// o reparar, hace falta algo que diagnosticar. Puesto a mano, es un interruptor más
        /// que alguien olvida al añadir una práctica.
        /// </summary>
        public bool NecesitaAveria()
        {
            if (Pasos == null) return false;
            for (int i = 0; i < Pasos.Count; i++)
            {
                if (Pasos[i] == null) continue;
                if (Pasos[i].Condicion == VdfCondicion.FallaDiagnosticada) return true;
                if (Pasos[i].Condicion == VdfCondicion.FallaReparada) return true;
                if (Pasos[i].Condicion == VdfCondicion.FallaReparadaYProbada) return true;
            }
            return false;
        }

        public VdfPasoPractica Paso(int i)
        {
            if (Pasos == null || i < 0 || i >= Pasos.Count) return null;
            return Pasos[i];
        }

        public List<string> Validar()
        {
            var problemas = new List<string>();

            if (Pasos == null || Pasos.Count == 0)
            {
                problemas.Add("La práctica no tiene ningún paso.");
                return problemas;
            }

            for (int i = 0; i < Pasos.Count; i++)
            {
                var p = Pasos[i];
                string donde = "Paso " + (i + 1) + ": ";

                if (p == null) { problemas.Add(donde + "está vacío."); continue; }

                if (string.IsNullOrEmpty(p.Instruccion))
                    problemas.Add(donde + "no tiene instrucción, así que el alumno no sabría qué hacer.");

                switch (p.Condicion)
                {
                    case VdfCondicion.ParametroEnValor:
                        if (string.IsNullOrEmpty(p.Objetivo))
                            problemas.Add(donde + "pide un parámetro pero no dice cuál.");
                        else
                        {
                            VfdParameterDef def;
                            if (!VfdParameterTable.TryGet(p.Objetivo, out def))
                                problemas.Add(donde + "el parámetro '" + p.Objetivo +
                                              "' no existe en la tabla.");
                        }
                        break;

                    case VdfCondicion.BornesUnidos:
                        if (string.IsNullOrEmpty(p.Objetivo) || !p.Objetivo.Contains("|"))
                            problemas.Add(donde + "debe indicar dos bornes separados por | .");
                        break;

                    case VdfCondicion.ComponenteIdentificado:
                    case VdfCondicion.GrupoIdentificado:
                        if (string.IsNullOrEmpty(p.Objetivo))
                            problemas.Add(donde + "no dice qué hay que identificar.");
                        break;

                    case VdfCondicion.NivelAlcanzado:
                        int n = Mathf.RoundToInt(p.Valor);
                        if (n < PlcFanLogic.NivelMinimo || n > PlcFanLogic.NivelMaximo)
                            problemas.Add(donde + "pide el nivel " + n + ", fuera del rango 1-" +
                                          PlcFanLogic.NivelMaximo + ".");
                        break;
                }
            }

            return problemas;
        }
    }
}
