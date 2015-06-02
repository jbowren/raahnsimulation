using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using OpenTK.Graphics.OpenGL;
using Raahn;

namespace RaahnSimulation
{
    [XmlRoot("Robot")]
    public class CarConfig
    {
        [XmlElement("X")]
        public double x;

        [XmlElement("Y")]
        public double y;

        [XmlElement("Angle")]
        public double angle;
    }

    public partial class Car : Entity
    {
        public const double HALF_QUERY_WIDTH = Simulator.WORLD_WINDOW_WIDTH / 2.0;
        public const double HALF_QUERY_HEIGHT = Simulator.WORLD_WINDOW_HEIGHT / 2.0;
        private const double CONTROL_THRESHOLD = 0.5;
        private const double ROTATE_SPEED = 2.0;
        private const double SPEED_X = 15.0;
        private const double SPEED_Y = 12.0;
        //Between 0 and twice ROTATE_SPEED
        private const double ROTATE_RANGE = 2.0 * ROTATE_SPEED;

        public List<Entity> entitiesHovering;
        private bool configLoaded;
        private uint rangeFinderCount;
        private uint pieSliceSensorCount;
        private QuadTree quadTree;
        private ControlScheme.SchemeFunction controlScheme;
        private List<List<uint>> modulationSignals;
        private List<ModulationScheme.SchemeFunction> modulationSchemes;
        private List<RangeFinderGroup> rangeFinderGroups;
        private List<PieSliceSensorGroup> pieSliceSensorGroups;
        private NeuralNetwork brain;

        public Car(Simulator sim, QuadTree tree) : base(sim)
        {
            texture = TextureManager.TextureType.CAR;
            type = EntityType.CAR;

            quadTree = tree;

            modulationSignals = new List<List<uint>>();
            modulationSchemes = new List<ModulationScheme.SchemeFunction>();
            rangeFinderGroups = new List<RangeFinderGroup>();
            pieSliceSensorGroups = new List<PieSliceSensorGroup>();

            speed.x = SPEED_X;
            speed.y = SPEED_Y;

            entitiesHovering = new List<Entity>();
        }

        //Updates only sensors.
        public void UpdateMinimal()
        {
            base.Update();

            for (int i = 0; i < rangeFinderGroups.Count; i++)
                rangeFinderGroups[i].Update();

            for (int i = 0; i < pieSliceSensorGroups.Count; i++)
                pieSliceSensorGroups[i].Update();
        }

        public override void Update()
        {
            if (controlScheme != null)
                controlScheme(this);

            double worldX = GetWorldX();
            double worldY = GetWorldY();

            Utils.Vector2 lowerLeft = camera.TransformWorld(worldX - HALF_QUERY_WIDTH, worldY - HALF_QUERY_HEIGHT);
            Utils.Vector2 upperRight = camera.TransformWorld(worldX + HALF_QUERY_WIDTH, worldY + HALF_QUERY_HEIGHT);

            AABB viewBounds = new AABB(upperRight.x - lowerLeft.x, upperRight.y - lowerLeft.y);
            viewBounds.Translate(lowerLeft.x, lowerLeft.y);

            Utils.LineSegment collisionLine = new Utils.LineSegment();

            Utils.Point2 original = new Utils.Point2(center.x, center.y);
            Utils.Point2 projected = new Utils.Point2(center.x + velocity.x, center.y + velocity.y);

            collisionLine.SetUp(original, projected);

            List<Entity> entitiesInBounds = quadTree.Query(viewBounds);
            bool canMove = true;

            for (int i = 0; i < entitiesInBounds.Count; i++)
            {
                if (entitiesInBounds[i].GetEntityType() == EntityType.WALL)
                {
                    Utils.LineSegment compare = ((Wall)entitiesInBounds[i]).GetLineSegment();
                    List<Utils.Point2> intersections = collisionLine.Intersects(compare);

                    //If there is a collision, don't move.
                    if (intersections.Count > 0)
                    {
                        canMove = false;
                        break;
                    }
                }
            }

            if (canMove)
            {
                drawingVec.x += velocity.x;
                drawingVec.y += velocity.y;
            }

            for (int i = 0; i < modulationSchemes.Count; i++)
                modulationSchemes[i](this, entitiesInBounds, modulationSignals[i]);

            brain.Train();

            base.Update();

            //Update sensors.
            for (int i = 0; i < rangeFinderGroups.Count; i++)
                rangeFinderGroups[i].Update();

            for (int i = 0; i < pieSliceSensorGroups.Count; i++)
                pieSliceSensorGroups[i].Update();
        }

        public override void UpdateEvent(Event e)
        {
            base.UpdateEvent(e);
        }

        public override void Draw()
        {
            base.Draw();

            GL.PushMatrix();

            GL.Translate(center.x, center.y, Utils.DISCARD_Z_POS);
            GL.Rotate(angle, 0.0, 0.0, 1.0);
            GL.Translate(-center.x, -center.y, -Utils.DISCARD_Z_POS);

            GL.Translate(drawingVec.x, drawingVec.y, Utils.DISCARD_Z_POS);
            GL.Scale(width, height, Utils.DISCARD_Z_SCALE);

            GL.DrawElements(mesh.GetRenderMode(), mesh.GetIndexCount(), DrawElementsType.UnsignedShort, IntPtr.Zero);

            GL.PopMatrix();

            for (int i = 0; i < rangeFinderGroups.Count; i++)
                rangeFinderGroups[i].Draw();

            for (int i = 0; i < pieSliceSensorGroups.Count; i++)
                pieSliceSensorGroups[i].Draw();
        }

        public override void DebugDraw()
        {
            base.DebugDraw();
        }

        public override void Clean()
        {
            RangeFinderGroup.Clean();
            PieSliceSensorGroup.Clean();
        }

        public void ResetBrain()
        {
            if (brain != null)
                brain.Reset();
        }

        public bool LoadConfig(string sensorFile, string networkFile)
        {
            //Even if the XML is invalid, the brain must be initiaized.
            brain = new NeuralNetwork();

            //If a configuration was already loaded delete the
            //VBOs and IBOs used as new ones will be allocated.
            if (configLoaded)
            {
                RangeFinderGroup.Clean();
                PieSliceSensorGroup.Clean();
                configLoaded = false;
            }

            if (!string.IsNullOrEmpty(sensorFile))
            {
                if (!InitSensors(sensorFile))
                    return false;
            }

            if (!string.IsNullOrEmpty(networkFile))
            {
                if (!InitBrain(networkFile))
                    return false;
            }

            configLoaded = true;

            return true;
        }

        public uint GetRangeFinderCount()
        {
            return rangeFinderCount;
        }

        public uint GetPieSliceSensorCount()
        {
            return pieSliceSensorCount;
        }

        private bool InitSensors(string sensorFile)
        {
            if (!File.Exists(sensorFile))
            {
                Console.WriteLine(string.Format(Utils.FILE_NOT_FOUND, sensorFile));
                return false;
            }

            TextReader configReader = new StreamReader(sensorFile);
            SensorConfig sensorConfig = null;

            try
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(SensorConfig));
                sensorConfig = (SensorConfig)deserializer.Deserialize(configReader);
            }
            catch (Exception e)
            {
                Console.WriteLine(Utils.XML_READ_ERROR);
                Console.WriteLine(Utils.SENSOR_LOAD_ERROR);
                Console.WriteLine(e.Message);

                return false;
            }
            finally
            {
                configReader.Close();
            }

            if (sensorConfig.rangeFinderGroups != null)
            {
                for (int i = 0; i < sensorConfig.rangeFinderGroups.Length; i++)
                {
                    RangeFinderGroupConfig current = sensorConfig.rangeFinderGroups[i];

                    if (current == null)
                        continue;

                    rangeFinderCount += current.count;

                    RangeFinderGroup rfg = new RangeFinderGroup(context, this, quadTree, current.count);
                    rfg.Configure(current.length, current.angleOffset, current.angleBetween);

                    if (current.entitiesToDetect != null)
                    {
                        for (int n = 0; n < current.entitiesToDetect.Length; n++)
                        {
                            Entity.EntityType type = Entity.GetTypeFromString(current.entitiesToDetect[n]);

                            if (type != Entity.EntityType.NONE)
                                rfg.AddEntityToDetect(type);
                        }
                    }

                    rangeFinderGroups.Add(rfg);
                }
            }

            if (sensorConfig.pieSliceSensorGroups != null)
            {
                for (int i = 0; i < sensorConfig.pieSliceSensorGroups.Length; i++)
                {
                    PieSliceSensorGroupConfig current = sensorConfig.pieSliceSensorGroups[i];

                    if (current == null)
                        continue;

                    pieSliceSensorCount += current.count;

                    PieSliceSensorGroup pieGroup = new PieSliceSensorGroup(context, this, quadTree);
                    pieGroup.AddSensors(current.count);
                    pieGroup.ConfigureSensors(current.maxDetection, current.angleOffset, current.angleBetween, current.outerRadius, current.innerRadius);

                    if (current.entitiesToDetect != null)
                    {
                        for (int n = 0; n < current.entitiesToDetect.Length; n++)
                        {
                            Entity.EntityType type = Entity.GetTypeFromString(current.entitiesToDetect[n]);

                            if (type != Entity.EntityType.NONE)
                                pieGroup.AddEntityToDetect(type);
                        }
                    }

                    pieSliceSensorGroups.Add(pieGroup);
                }
            }

            return true;
        }

        private bool InitBrain(string networkFile)
        {
            if (!File.Exists(networkFile))
            {
                Console.WriteLine(Utils.FILE_NOT_FOUND, networkFile);
                return false;
            }

            TextReader configReader = new StreamReader(networkFile);
            NeuralNetworkConfig networkConfig = null;

            try
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(NeuralNetworkConfig));
                networkConfig = (NeuralNetworkConfig)deserializer.Deserialize(configReader);
            }
            catch (Exception e)
            {
                Console.WriteLine(Utils.XML_READ_ERROR);
                Console.WriteLine(Utils.NETWORK_LOAD_ERROR);
                Console.WriteLine(e.Message);

                return false;
            }
            finally
            {
                configReader.Close();
            }

            //No neuron groups, connection groups, or control scheme.
            //return true to continue without them.
            if (networkConfig.neuronGroups == null)
            {
                Console.WriteLine(Utils.NO_NEURON_GROUPS);
                return true;
            }

            if (networkConfig.connectionGroups == null)
            {
                Console.WriteLine(Utils.NO_CONNECTION_GROUPS);
                return true;
            }

            if (networkConfig.controlScheme == null)
            {
                Console.WriteLine(Utils.NO_CONTROL_SCHEME);
                return true;
            }

            ControlScheme.Scheme cDescriptor = ControlScheme.GetSchemeFromString(networkConfig.controlScheme);

            if (cDescriptor == ControlScheme.Scheme.NONE)
            {
                Console.WriteLine(Utils.NO_CONTROL_SCHEME);
                return true;
            }

            controlScheme = ControlScheme.GetSchemeFunction(cDescriptor);

            //Save the modulation descriptions in addition to the functions
            //because the parameter interpreting function takes the descriptions.
            List<ModulationScheme.Scheme> mDescriptions = new List<ModulationScheme.Scheme>();

            int[] neuronGroupIds = new int[networkConfig.neuronGroups.Length];

            //Add each neuron group.
            for (uint i = 0; i < networkConfig.neuronGroups.Length; i++)
            {
                NeuronGroupConfig nGroupConfig = networkConfig.neuronGroups[(int)i];

                NeuronGroup.Type type = Utils.GetGroupTypeFromString(nGroupConfig.type);

                neuronGroupIds[(int)i] = brain.AddNeuronGroup(nGroupConfig.count, type);
            }

            //Add each connection group.
            for (uint i = 0; i < networkConfig.connectionGroups.Length; i++)
            {
                ConnectionConfig cGroupConfig = networkConfig.connectionGroups[(int)i];

                uint inputGroupIndex = GetIdIndex(cGroupConfig.inputGroupId, networkConfig.neuronGroups);
                uint outputGroupIndex = GetIdIndex(cGroupConfig.outputGroupId, networkConfig.neuronGroups);

                //Make sure the neuron groups to be connected exist.
                if (inputGroupIndex < networkConfig.neuronGroups.Length
                    && outputGroupIndex < networkConfig.neuronGroups.Length)
                {
                    NeuronGroup.Identifier inputGroup;
                    inputGroup.index = neuronGroupIds[(int)inputGroupIndex];
                    string inputTypeString = networkConfig.neuronGroups[(int)inputGroupIndex].type;
                    inputGroup.type = Utils.GetGroupTypeFromString(inputTypeString);

                    NeuronGroup.Identifier outputGroup;
                    outputGroup.index = neuronGroupIds[(int)outputGroupIndex];
                    string outputTypeString = networkConfig.neuronGroups[(int)outputGroupIndex].type;
                    outputGroup.type = Utils.GetGroupTypeFromString(outputTypeString);

                    ConnectionGroup.TrainFunctionType trainMethod = Utils.GetMethodFromString(cGroupConfig.trainingMethod);

                    //Check if a modulation scheme is specified.
                    ModulationScheme.Scheme mDescriptor;

                    if (!string.IsNullOrEmpty(cGroupConfig.modulationScheme))
                        mDescriptor = ModulationScheme.GetSchemeFromString(cGroupConfig.modulationScheme);
                    else
                        mDescriptor = ModulationScheme.Scheme.NONE;

                    //If a scheme is specified, add a signal for it.
                    if (mDescriptor != ModulationScheme.Scheme.NONE)
                    {
                        uint modSig = 0;
                        modSig = ModulationSignal.AddSignal();

                        ModulationScheme.SchemeFunction mFunction = ModulationScheme.GetSchemeFunction(mDescriptor);

                        //Check if the modulation scheme function has already 
                        //been added to the list of modulation scheme functions.
                        bool hasFunction = false;

                        for (int n = 0; n < modulationSchemes.Count; n++)
                        {
                            if (modulationSchemes[n] == mFunction)
                            {
                                modulationSignals[n].Add(modSig);

                                hasFunction = true;
                                break;
                            }
                        }

                        //If the modulation scheme function was not added
                        //add it to the list of scheme functions.
                        if (!hasFunction)
                        {
                            mDescriptions.Add(mDescriptor);

                            modulationSchemes.Add(mFunction);
                            modulationSignals.Add(new List<uint>());

                            modulationSignals[modulationSignals.Count - 1].Add(modSig);
                        }

                        brain.ConnectGroups(inputGroup, outputGroup, trainMethod, (int)modSig, 
                                            cGroupConfig.learningRate, cGroupConfig.useBias);
                    }
                    else
                        brain.ConnectGroups(inputGroup, outputGroup, trainMethod, ModulationSignal.INVALID_INDEX, 
                                            cGroupConfig.learningRate, cGroupConfig.useBias);
                }
            }

            //Interpret the parameters of the network file
            //for the control scheme and the modulation schemes.
            if (networkConfig.parameters != null)
                ControlScheme.InterpretParameters(networkConfig.parameters, cDescriptor);

            for (int i = 0; i < mDescriptions.Count; i++)
                ModulationScheme.InterpretParameters(networkConfig.parameters, mDescriptions[i]);

            return true;
        }

        private uint GetIdIndex(uint id, NeuronGroupConfig[] neuronGroups)
        {
            for (uint i = 0; i < neuronGroups.Length; i++)
            {
                if (id == neuronGroups[(int)i].id)
                    return i;
            }

            //Return the first index if the id is not found.
            return 0;
        }

        private Utils.Point2 GetNearestIntersection(List<Utils.Point2> intersections)
        {
            Utils.Point2 nearest = intersections[0];

            for (int x = 1; x < intersections.Count; x++)
            {
                Utils.Point2 currentIntersection = intersections[x];
                Utils.Point2 centerPoint = new Utils.Point2(center.x, center.y);

                if (Utils.GetDist(nearest, centerPoint) > Utils.GetDist(currentIntersection, centerPoint))
                    nearest = intersections[x];
            }

            return nearest;
        }
    }
}
