
using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Lleva una práctica: mantiene el contexto, registra los logros y dice en qué paso va
    /// el alumno.
    ///
    /// Es la única pieza de este sistema que toca la escena. Todo lo que decide si un paso
    /// está cumplido vive en <see cref="VdfEvaluadorPasos"/>, que es C# puro y está probado
    /// aparte; aquí solo se le da de comer el estado real y se le pregunta.
    ///
    /// Sirve igual en VR y en escritorio: no sabe nada de ratones ni de mandos.
    /// </summary>
    public class VdfPracticaRunner : MonoBehaviour
    {
        [Header("Qué práctica se está haciendo")]
        public VdfPractica Practica;

        [Header("Referencias (se buscan solas si se dejan vacías)")]
        public VdfSimulacion Simulacion;
        public VdfJumperController Cableado;

        [Tooltip("Cableado de VR. Se usa cuando la escena no lleva el de ratón.")]
        public VdfXrWiring CableadoXr;

        [Tooltip("Id de la avería a provocar en la práctica 5. Vacío = una al azar.")]
        public string FallaForzada;

        /// <summary>Se dispara cuando cambia el paso actual o el estado de algún paso.</summary>
        public event System.Action ProgresoCambiado;

        private VdfContextoPractica _ctx;
        private int _pasoAnterior = -2;

        /// <summary>Cuándo se intentó arrancar el motor en una práctica que no lo deja (-1: nunca).</summary>
        private float _intentoDeMarcha = -1f;

        /// <summary>Segundos que la guía explica por qué el motor no arrancó.</summary>
        public const float SegundosAvisoMarcha = 6f;

        public const string AvisoMarchaBloqueada =
            "En esta práctica el motor no se arranca: aquí solo se configura. Se pone en marcha en la práctica 4.";

        /// <summary>El estado que lee el evaluador. Nunca es null tras el Awake.</summary>
        public VdfContextoPractica Contexto
        {
            get
            {
                if (_ctx == null) _ctx = new VdfContextoPractica();
                return _ctx;
            }
        }

        /// <summary>El cableado que manda en esta escena, venga del ratón o del visor.</summary>
        public IVdfFuenteCableado Cables
        {
            get
            {
                // El de VR manda cuando existe: una escena con equipo de visor puede llevar
                // además el controlador de ratón (la de VR lo lleva desde la fase 4), y si
                // ganaba el de ratón, lo que el alumno enchufaba con las manos iba a un grafo
                // que nadie miraba. Al revés no hay riesgo: la escena de escritorio no tiene
                // puente de VR.
                if (CableadoXr != null) return CableadoXr;
                return Cableado;
            }
        }

        private void Awake()
        {
            if (Simulacion == null) Simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            if (Cableado == null) Cableado = Object.FindAnyObjectByType<VdfJumperController>();
            if (CableadoXr == null) CableadoXr = Object.FindAnyObjectByType<VdfXrWiring>();

            Contexto.Reiniciar();
            IndexarGrupos();
            Suscribir();

            Practica = LaDelMenu(Practica);

            _arrancando = true;
            if (Practica == null) PracticaPorDefecto();
            else Empezar(Practica);
            _arrancando = false;
        }

        /// <summary>Verdadero mientras corre Awake: ver el comentario de arriba.</summary>
        private bool _arrancando;

        private int _inicializacionesAlEmpezar;
        private bool _averiaProvocada;

        public static int IndiceDondeEmpiezaLaAveria(VdfPractica practica)
        {
            if (practica == null || practica.Pasos == null) return 0;
            for (int i = 0; i < practica.Pasos.Count; i++)
                if (practica.Pasos[i] != null && practica.Pasos[i].Condicion == VdfCondicion.FallaDiagnosticada)
                    return Mathf.Max(0, i - 1);
            return 0;
        }

        /// <summary>Provoca ya la avería de la práctica. Lo hace solo el runner al llegar a su paso; los tests también.</summary>
        public void ProvocarAveriaAhora()
        {
            ProvocarAveria(Practica);
            _averiaProvocada = true;
        }

        private VfdKeypad TecladoDelVariador()
        {
            if (Simulacion == null || Simulacion.Operador == null) return null;
            return Simulacion.Operador.Teclado;
        }

        private static VdfPractica LaDelMenu(VdfPractica practica)
        {
            if (practica == null) return null;
            var menu = Object.FindAnyObjectByType<VdfMenuPracticas>();
            if (menu == null || menu.Practicas == null) return practica;

            foreach (var p in menu.Practicas)
                if (p != null && p.Numero == practica.Numero) return p;
            return practica;
        }

        private void PracticaPorDefecto()
        {
            var menu = Object.FindAnyObjectByType<VdfMenuPracticas>();
            if (menu == null || menu.Practicas == null) return;

            for (int i = 0; i < menu.Practicas.Count; i++)
            {
                if (menu.Practicas[i] == null) continue;
                Empezar(menu.Practicas[i]);
                return;
            }
        }

        private void Suscribir()
        {
            if (Cableado == null) return;
            Cableado.BorneSenalado -= AlSenalarBorne;
            Cableado.BorneSenalado += AlSenalarBorne;
        }

        private void OnDestroy()
        {
            if (Cableado != null) Cableado.BorneSenalado -= AlSenalarBorne;
        }

        private void AlSenalarBorne(VdfTerminal borne)
        {
            if (borne != null) Identificar(borne.Id);
        }

        /// <summary>
        /// Rellena qué componentes tiene cada grupo del tablero, leyéndolo de la escena.
        ///
        /// Se excluyen las etiquetas: son serigrafía, no componentes, y pedirle al alumno
        /// que «identifique» un rótulo no tiene sentido. Si no se filtraran, el grupo
        /// "Indicadores de velocidad" exigiría señalar también su cartel VELOCIDAD.
        /// </summary>
        public void IndexarGrupos()
        {
            Contexto.Grupos.Clear();

            var bornes = Object.FindObjectsByType<VdfTerminal>();
            for (int i = 0; i < bornes.Length; i++)
            {
                var t = bornes[i];
                if (t == null) continue;
                Agrupar(t.Grupo, t.Id);
            }

            var mandos = Object.FindObjectsByType<VdfPulsadorTablero>();
            for (int i = 0; i < mandos.Length; i++)
            {
                var m = mandos[i];
                if (m == null) continue;
                // Los interruptores no son pulsadores del tablero: no cuentan para «señala
                // los cinco pulsadores de mando».
                // El mando de la práctica del laboratorio: los tres interruptores y la
                // parada de emergencia, que es lo único del tablero que se sigue usando.
                if (m.EsInterruptor || m.Accion == VdfAccionPulsador.Emergencia)
                    Agrupar(VdfPracticasDefaults.GrupoMandoLaboratorio, m.gameObject.name);
                if (m.EsInterruptor) continue;
                Agrupar("Mando", m.gameObject.name);
            }
        }

        /// <summary>Mete un id en su grupo, sin repetirlo.</summary>
        private void Agrupar(string grupo, string id)
        {
            if (string.IsNullOrEmpty(grupo) || string.IsNullOrEmpty(id)) return;

            List<string> lista;
            if (!Contexto.Grupos.TryGetValue(grupo, out lista))
            {
                lista = new List<string>();
                Contexto.Grupos[grupo] = lista;
            }
            if (!lista.Contains(id)) lista.Add(id);
        }

        private void Update()
        {
            if (Simulacion != null)
            {
                Contexto.Vfd = Simulacion.Vfd;
                Contexto.Plc = Simulacion.Plc;
                Contexto.InformeCableado = Simulacion.InformeCableado;
            }
            // El grafo puede venir del ratón o del visor. Mirando solo al primero, en la
            // escena de VR el contexto se quedaba sin grafo y los pasos de unión de la
            // práctica 3 no se podían dar por cumplidos ni cableándolo todo bien.
            var fuente = Cables;
            if (fuente != null) Contexto.Grafo = fuente.Grafo;

            // Lo hecho en el teclado del variador: inicializar y entrar a ver parámetros.
            if (Simulacion != null && Simulacion.Vfd != null
                && Simulacion.Vfd.Inicializaciones > _inicializacionesAlEmpezar)
                Contexto.VariadorInicializado = true;
            var teclado = TecladoDelVariador();
            if (teclado != null)
                foreach (var id in teclado.ParametrosVistos) Contexto.ParametrosVistos.Add(id);

            // En reconocimiento y configuración el motor no arranca, ni con RUN ni con el
            // PLC. Se apunta el intento para que la guía diga por qué no pasa nada.
            if (Simulacion != null && Simulacion.Vfd != null)
            {
                bool bloquear = Practica != null && Practica.BloqueaMarcha;
                Simulacion.Vfd.MarchaBloqueada = bloquear;
                if (bloquear && Simulacion.Vfd.PideMarcha())
                {
                    _intentoDeMarcha = Time.time;
                    // Que el RUN no quede «pendiente» y arranque al cambiar de práctica.
                    Simulacion.Vfd.MarchaDesdeOperador = false;
                }
            }

            // La avería llega cuando el guion lo dice, no antes.
            if (!_averiaProvocada && Practica != null && Practica.NecesitaAveria())
            {
                int paso = PasoActual();
                if (paso < 0 || paso >= IndiceDondeEmpiezaLaAveria(Practica)) ProvocarAveriaAhora();
            }

            // Los logros se registran en el momento en que ocurren: uno que no se capture
            // cuando pasa, se pierde para siempre.
            Contexto.ActualizarLogros();

            int actual = PasoActual();
            if (actual != _pasoAnterior)
            {
                _pasoAnterior = actual;
                var h = ProgresoCambiado;
                if (h != null) h();
            }
        }

        // =====================================================================================
        // Consulta
        // =====================================================================================

        public int TotalPasos { get { return Practica != null ? Practica.TotalPasos : 0; } }

        public bool PasoCumplido(int i)
        {
            if (Practica == null) return false;
            return VdfEvaluadorPasos.Cumple(Practica.Paso(i), Contexto);
        }

        /// <summary>Índice del primer paso sin cumplir, o -1 si están todos.</summary>
        public int PasoActual()
        {
            if (Practica == null) return -1;

            for (int i = 0; i < Practica.TotalPasos; i++)
                if (!PasoCumplido(i)) return i;
            return -1;
        }

        public bool Completada { get { return Practica != null && PasoActual() < 0; } }

        public int PasosCumplidos
        {
            get
            {
                int n = 0;
                for (int i = 0; i < TotalPasos; i++) if (PasoCumplido(i)) n++;
                return n;
            }
        }

        /// <summary>
        /// Texto de apoyo del paso actual: su progreso si es de grupo, o lo que falta del
        /// cableado si el paso lo exige. Vacío si no hay nada útil que decir.
        /// </summary>
        public string DetalleDelPasoActual()
        {
            if (_intentoDeMarcha >= 0f && Time.time - _intentoDeMarcha < SegundosAvisoMarcha)
                return AvisoMarchaBloqueada;

            int i = PasoActual();
            if (Practica == null || i < 0) return "";

            var paso = Practica.Paso(i);
            if (paso == null) return "";

            string error = AvisoDeSenaladoErroneo();
            if (error != null) return error;

            if (paso.Condicion == VdfCondicion.GrupoIdentificado)
            {
                int total;
                int hechos = VdfEvaluadorPasos.ProgresoDeGrupo(paso.Objetivo, Contexto, out total);
                if (hechos >= 0) return "Identificados " + hechos + " de " + total + ".";
            }

            if (paso.Condicion == VdfCondicion.FallaReparadaYProbada && Contexto.FallaActiva != null)
            {
                if (!Contexto.FallaActiva.Reparada(Contexto.Vfd, Contexto.Grafo))
                    return "La avería sigue ahí.";
                return "Reparada. Ahora sube hasta la velocidad 4: vas por la " + Contexto.NivelMaximoTrasReparar + ".";
            }

            if (paso.Condicion == VdfCondicion.ConfiguracionSinAvisos && Contexto.Vfd != null)
            {
                var avisos = Contexto.Vfd.ValidarConfiguracion();
                if (avisos.Count > 0)
                    return (avisos.Count == 1 ? "Queda 1 aviso." : "Quedan " + avisos.Count + " avisos.")
                           + (char)10 + "→ " + avisos[0].ParametroId + ": " + avisos[0].Mensaje;
            }

            if (paso.Condicion == VdfCondicion.CableadoCorrecto && Contexto.InformeCableado != null)
            {
                var inf = Contexto.InformeCableado;
                if (!inf.EsCorrecto)
                {
                    string s = "Faltan " + inf.Faltantes.Count + " uniones";
                    if (inf.Sobrantes.Count > 0) s += " y sobran " + inf.Sobrantes.Count;
                    if (inf.Faltantes.Count > 0) s += ".\n→ " + Legible(inf.Faltantes[0]);
                    return s;
                }
            }

            if (paso.Condicion == VdfCondicion.BornesUnidos && Contexto.InformeCableado != null
                && Contexto.InformeCableado.Sobrantes.Count > 0)
            {
                return "Sobra un cable. Quítalo (clic derecho, o cógelo y suéltalo en el aire):"
                       + (char)10 + "→ " + Legible(Contexto.InformeCableado.Sobrantes[0]);
            }

            return "";
        }

        /// <summary>
        /// Los ids internos de los bornes del breaker por como están rotulados: F1, F2 y el
        /// del centro, que no lleva rótulo. El resto ya coincide con el tablero.
        /// </summary>
        public static string Legible(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return texto;
            return texto.Replace("BRK-L1", "F1").Replace("BRK-L3", "F2")
                        .Replace("BRK-L2", "el borne del centro del breaker");
        }

        // =====================================================================================
        // Acciones del alumno
        // =====================================================================================

        /// <summary>
        /// Registra que el alumno ha señalado un componente. Lo llaman el clic del ratón en
        /// escritorio y el rayo o el dedo en VR: a este objeto le da igual de dónde venga.
        /// </summary>
        public void Identificar(string id)
        {
            if (!CuentaParaElPasoActual(id))
            {
                _senaladoErroneo = id;
                _instanteError = Time.time;
                return;
            }
            _senaladoErroneo = null;
            Contexto.Identificar(id);
        }

        /// <summary>Lo último que se señaló sin ser lo que pide el paso, y cuándo.</summary>
        private string _senaladoErroneo;
        private float _instanteError = -100f;
        public const float SegundosAvisoError = 5f;

        /// <summary>
        /// El aviso de «eso no es» para el paso actual, o null. Dice qué se ha señalado, para
        /// que el alumno aprenda también de la pieza equivocada.
        /// </summary>
        public string AvisoDeSenaladoErroneo()
        {
            if (_senaladoErroneo == null || Time.time - _instanteError > SegundosAvisoError) return null;
            int i = PasoActual();
            if (Practica == null || i < 0) return null;
            var paso = Practica.Paso(i);
            if (paso == null) return null;
            if (paso.Condicion != VdfCondicion.ComponenteIdentificado && paso.Condicion != VdfCondicion.GrupoIdentificado)
                return null;
            string nombre = VdfEtiquetaFlotante.NombreDeId(_senaladoErroneo);
            if (!string.IsNullOrEmpty(nombre)) nombre = nombre.Split((char)10)[0];
            return "Eso es " + (string.IsNullOrEmpty(nombre) ? _senaladoErroneo : nombre) + ", no lo que se pide.";
        }

        private bool CuentaParaElPasoActual(string id)
        {
            int i = PasoActual();
            if (Practica == null || i < 0) return true;

            var paso = Practica.Paso(i);
            if (paso == null) return true;

            switch (paso.Condicion)
            {
                case VdfCondicion.ComponenteIdentificado:
                    foreach (var o in (paso.Objetivo ?? "").Split('|'))
                        if (o.Trim() == id) return true;
                    return false;

                case VdfCondicion.GrupoIdentificado:
                    List<string> miembros;
                    return Contexto.Grupos.TryGetValue(paso.Objetivo ?? "", out miembros)
                           && miembros.Contains(id);

                default:
                    return true;
            }
        }

        /// <summary>Marca el paso actual si es de los que el alumno confirma al leerlos.</summary>
        public bool ConfirmarPasoManual()
        {
            int i = PasoActual();
            if (Practica == null || i < 0) return false;

            var paso = Practica.Paso(i);
            if (paso == null || paso.Condicion != VdfCondicion.Manual) return false;

            Contexto.Identificar(VdfEvaluadorPasos.ClaveManual(paso));
            return true;
        }

        /// <summary>Empieza (o reinicia) la práctica indicada.</summary>
        public void Empezar(VdfPractica practica)
        {
            // El guion según con qué se opere: los pasos de pulsadores o los de
            // interruptores. La práctica guarda los dos y aquí solo se elige.
            if (practica != null && Simulacion != null) practica.UsarMando(Simulacion.ModoMando);

            Practica = practica;
            Contexto.Reiniciar();
            _senaladoErroneo = null;
            IndexarGrupos();
            _pasoAnterior = -2;

            Suscribir();

            if (practica != null && Simulacion != null)
            {
                Simulacion.SaltarConfiguracion = practica.EmpezarConfigurado;
                Simulacion.SaltarCableado = practica.EmpezarCableado;

                // Hay que APLICARLO, no solo apuntarlo: estos dos flags los leía Start() una
                // sola vez, así que cambiar de práctica no cambiaba el estado del módulo.
                if (Application.isPlaying && !_arrancando) Simulacion.AplicarPuntoDePartida();

                // Y estropearlo, si la práctica es de diagnóstico... pero CUANDO TOCA, no al
                // empezar. Ver IndiceDondeEmpiezaLaAveria.
                Contexto.FallaActiva = null;
                _averiaProvocada = false;
                if (practica.NecesitaAveria() && IndiceDondeEmpiezaLaAveria(practica) <= 0)
                    ProvocarAveriaAhora();
            }

            // Lo que se hizo en el teclado en otra práctica no cuenta en esta.
            _inicializacionesAlEmpezar = Simulacion != null && Simulacion.Vfd != null
                ? Simulacion.Vfd.Inicializaciones : 0;
            var tecladoAlEmpezar = TecladoDelVariador();
            if (tecladoAlEmpezar != null) tecladoAlEmpezar.OlvidarVistos();

            // En reconocimiento se señala para identificar, no para cablear.
            if (practica != null && Cableado != null)
                Cableado.PermitirCablear = practica.PermiteCablear;

            // En el visor la bandera del raton no basta: los cables son objetos que se
            // cogen con el mando y no preguntan a nadie. Hay que apagarles el agarre.
            if (practica != null && CableadoXr != null)
            {
                CableadoXr.PermitirCoger(practica.PermiteCablear);

                CableadoXr.MostrarCables(practica.PermiteCablear || practica.EmpezarCableado);
            }
        }

        public void Reiniciar()
        {
            Empezar(Practica);
        }

        /// <summary>
        /// Estropea el módulo si la práctica lo pide, y apunta qué avería es.
        ///
        /// Sin esto la práctica de diagnóstico no se podía terminar, y no por un fallo
        /// sutil: nadie asignaba nunca la avería activa, así que el paso «di cuál crees que
        /// es la causa» no se cumplía ni acertando, porque no había causa. El catálogo de
        /// seis averías estaba escrito y probado, pero el alumno no llegaba a verlas.
        ///
        /// Va después de <c>AplicarPuntoDePartida</c>, que deja el módulo sano: primero se
        /// repara todo, luego se rompe una cosa.
        /// </summary>
        private void ProvocarAveria(VdfPractica practica)
        {
            Contexto.FallaActiva = null;
            if (practica == null || !practica.NecesitaAveria()) return;

            var falla = VdfCatalogoFallas.Elegir(FallaForzada);
            if (falla == null) return;

            var fuente = Cables;
            falla.Aplicar(Simulacion.Vfd, fuente != null ? fuente.Grafo : null);
            Contexto.FallaActiva = falla;

            // En el visor, el cable que «falta» tiene que verse suelto: ver
            // VdfXrWiring.DesenchufarUnExtremo.
            if (falla.Tipo == VdfTipoFalla.CableFaltante && Simulacion.CableadoXr != null)
                Simulacion.CableadoXr.DesenchufarUnExtremo(falla.ObjetivoA, falla.ObjetivoB);

            // Que la simulación se entere del cambio de cableado, si la avería fue de cable.
            Simulacion.RevisarCableado();
        }
    }
}
