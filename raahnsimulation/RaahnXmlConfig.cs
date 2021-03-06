using System.Xml.Serialization;

namespace RaahnSimulation
{
	[XmlRoot("NeuralNetwork")]
	public class NeuralNetworkConfig
	{
        [XmlAttribute("UseNovelty")]
        public bool useNovelty;

		[XmlElement("HistoryBufferSize")]
		public uint historyBufferSize;

		[XmlElement("WeightCap")]
		public double weightCap;

		[XmlElement("OutputNoiseMagnitude")]
		public double outputNoiseMagnitude;

        [XmlElement("WeightNoiseMagnitude")]
        public double weightNoiseMagnitude;

		[XmlElement("ControlScheme")]
		public string controlScheme;

		[XmlElement("Parameter")]
		public string[] parameters;

		[XmlElement("NeuronGroup")]
		public NeuronGroupConfig[] neuronGroups;

		[XmlElement("ConnectionGroup")]
		public ConnectionConfig[] connectionGroups;
	}

	[XmlRoot("NeuronGroup")]
	public class NeuronGroupConfig
	{
		[XmlAttribute("Id")]
		public uint id;

		[XmlElement("Count")]
		public uint count;

		[XmlElement("Type")]
		public string type;
	}

	[XmlRoot("ConnectionGroup")]
	public class ConnectionConfig
	{
		[XmlElement("InputGroup")]
		public uint inputGroupId;

		[XmlElement("OutputGroup")]
		public uint outputGroupId;

		[XmlElement("SamplesPerTick")]
		public uint samplesPerTick;

		[XmlAttribute("UseBias")]
		public bool useBias;

		[XmlElement("LearningRate")]
		public double learningRate;

		[XmlElement("TrainingMethod")]
		public string trainingMethod;

		[XmlElement("ModulationScheme")]
		public string modulationScheme;

		public ConnectionConfig()
		{
			useBias = false;
		}
	}
}