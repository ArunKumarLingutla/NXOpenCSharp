using NXOpen.UF;
using NXOpen;
using System;
using System.Collections.Generic;
using NXOpenUI;
using NXOpen.Assemblies;
using System.Linq;
using NXOpen.Annotations;
using NXOpen.Features;
using static NXOpen.BodyDes.OnestepUnformBuilder;
using static NXOpen.Tooling.ScrapDesignBuilder;
using System.IO;
using Body = NXOpen.Body;
using Part = NXOpen.Part;
using NXOpen.Markup;
using NXOpen.Utilities;
using static NXOpen.Features.SectionSurfaceBuilderEx;
using static NXOpen.UF.UFModl;

namespace NXOpenCS
{
    public class Source
    {
        //Taking NXSession, and collecting work and display parts from NXSession
        public static Session theSession;
        public static Part workPart;
        public static Part displayPart;
        public static ListingWindow lw;
        public static UFSession theUFSession;
        public static DisplayManager theDisplayManager;
        public static UI theUI;

        //Collecting Bodies, Faces, and Edges and storing them in the below lists
        public static List<Body> lsBodies = new List<Body>();
        public static List<Face> lsFaces = new List<Face>();
        public static List<Edge> lsEdgesFromFaces = new List<Edge>();
        public static List<Edge> lsEdgesFromBodies = new List<Edge>(); //will give exact no of edges without duplicate

        List<NXOpen.Assemblies.Component> assemblies;
        List<NXOpen.Assemblies.Component> components;

        public static Line line;

        public Source()
        {
            //Initialising the session and parts from NX when ever Object is created for source class
            theSession = NXOpen.Session.GetSession();
            workPart = theSession.Parts.Work;
            displayPart = theSession.Parts.Display;
            theDisplayManager = theSession.DisplayManager;
            lw = theSession.ListingWindow;
            lw.Open();
            theUFSession = NXOpen.UF.UFSession.GetUFSession();
            theUI = NXOpen.UI.GetUI();
        }

        public void GetAllComponents()
        {
            //Get all parts open in the session
            //If it is an assembly, it gets all the components along with assembly, if ypu have 2 parts in an assembly it will give 3
            //PartCollection partList=theSession.Parts;
            //lw.WriteLine(Convert.ToString(partList.ToArray().Length));

            //Getting all the sub assemblies and components in the assembly
            assemblies = new List<Component>();
            components = new List<Component>();

            try
            {
                //If the display part is not an assembly than root component will be null
                if (displayPart.ComponentAssembly.RootComponent is null)
                {
                    lw.WriteLine("It is not an assembly");
                }
                else
                {
                    NXOpen.Assemblies.Component rootComponent = displayPart.ComponentAssembly.RootComponent;

                    //Going layer by level by level and getting all the child components
                    //storing current level components into "currentLevelComponents" list and traversing them to get their child components
                    //storing the child components in allChildComponents and making those child components into current level components and continuing the process
                    List<Component> currentLevelComponents = rootComponent.GetChildren().ToList();
                    List<Component> allChildComponents = rootComponent.GetChildren().ToList();
                    while (true)
                    {
                        //Getting child components of current level and adding it to allChildComponents, if there are no child components loop terminates
                        List<Component> childComponentsOfCurrentLevelComponents = currentLevelComponents.SelectMany(x => x.GetChildren()).ToList();
                        if (childComponentsOfCurrentLevelComponents.Count == 0) break;
                        allChildComponents.AddRange(childComponentsOfCurrentLevelComponents);
                        currentLevelComponents = childComponentsOfCurrentLevelComponents;
                    }

                    //saparating assemblies and individual components from allChildComponents
                    assemblies = allChildComponents.Where(x => x.GetChildren().Length != 0).ToList();
                    components = allChildComponents.Where(x => x.GetChildren().Length == 0).ToList();


                }
            }
            catch (Exception ex)
            {
                theUI.NXMessageBox.Show("Error", NXMessageBox.DialogType.Error, Convert.ToString(ex.Message));
            }
        }

        public void GetBodies()
        {
            try
            {
                //If the 
                if (displayPart != null && displayPart.ComponentAssembly.RootComponent is null)
                {
                    lsBodies.AddRange(displayPart.Bodies.ToArray());
                }
                else
                {
                    GetAllComponents();
                    foreach (Component component in components)
                    {
                        Part part = component.Prototype as Part;
                        lsBodies.AddRange(part.Bodies.ToArray());
                    }
                }
            }
            catch (Exception ex) {
                theUI.NXMessageBox.Show("Error", NXMessageBox.DialogType.Error, Convert.ToString(ex.Message));
            }
        }

        public void GetFaces()
        {
            if (lsBodies.Count == 0)
            {
                GetBodies();
            }

            foreach (Body body in lsBodies)
            {
                lsFaces.AddRange(body.GetFaces());
            }
        }
        public void GetBodiesAndFacesFromAssembly(out List<Body> bodies,out List<Face> faces)
        {
            //This will collect all the solid bodies from the display doesn't matter wether it is in assembly or part

            bodies = new List<Body>(); 
            faces= new List<Face>(); 

            Tag body=Tag.Null;

            theUFSession.Obj.CycleObjsInPart(displayPart.Tag, UFConstants.UF_solid_type, ref body);
            while (body != Tag.Null)
            {
                NXObject nXObject = (NXObject)NXObjectManager.Get(body);
                if (nXObject is Body) { 
                    int bodyType; 
                    theUFSession.Modl.AskBodyType(body, out bodyType);
                    if (bodyType is UFConstants.UF_MODL_SOLID_BODY){ 
                        Body bdy = (Body)NXObjectManager.Get(body);
                        if (bdy != null) { 
                            bodies.Add(bdy); 
                            faces.AddRange(bdy.GetFaces()); 
                        }
                    }
                }
                theUFSession.Obj.CycleObjsInPart(displayPart.Tag, UFConstants.UF_solid_type, ref body);
            }
            lw.WriteLine($"no of bodies: {bodies.Count}");
            lw.WriteLine($"no of faces: {faces.Count}");

        }
        public void GetEdges()
        {
            if (lsBodies.Count == 0)
            {
                GetBodies();
            }
            foreach (Face face in lsFaces)
            {
                lsEdgesFromFaces.AddRange(face.GetEdges());
            }
            foreach (Body body in lsBodies)
            {
                lsEdgesFromBodies.AddRange(body.GetEdges());
            }
        }
        public void DisplayInfoInLW()
        {
            GetBodies();
            GetFaces();
            GetEdges();
            lw.WriteLine($"No Of Bodies: {lsBodies.Count}");
            lw.WriteLine($"No Of Faces: {lsFaces.Count}");
            lw.WriteLine($"No Of Edges: {lsEdgesFromBodies.Count}");
            if (!(displayPart.ComponentAssembly.RootComponent is null))
            {
                if (assemblies.Count > 0)
                {
                    lw.WriteLine("Assemblies in " + displayPart.ComponentAssembly.RootComponent.DisplayName + " are: ");
                    foreach (Component component in assemblies)
                    {
                        lw.WriteLine(component.DisplayName);
                    }
                }
                if (components.Count > 0)
                {
                    lw.WriteLine("Components in " + displayPart.ComponentAssembly.RootComponent.DisplayName + " are: ");
                    foreach (Component component in components)
                    {
                        lw.WriteLine(component.DisplayName);
                    }
                }
            }
        }
        public void AddComponentsToAssembly()
        {
            try
            {
                string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!Directory.Exists(folderPath))
                {
                    lw.WriteLine("Folder does not exists...");
                }
                else
                {
                    string[] partFiles = Directory.GetFiles(folderPath, "*.prt");
                    if (partFiles.Length == 0)
                    {
                        lw.WriteLine($"No part files present in {folderPath}");
                    }
                    else
                    {
                        foreach (string partFile in partFiles)
                        {
                            string referenceSetName = "Entire Part";
                            string componentName = Path.GetFileNameWithoutExtension(partFile);
                            Point3d basePoint = new Point3d(0, 0, 0);
                            Matrix3x3 wcsMatrix = workPart.WCS.CoordinateSystem.Orientation.Element;
                            int layer = -1;
                            PartLoadStatus partLoadStatus;
                            Component newComponent = workPart.ComponentAssembly.AddComponent
                            (partFile, referenceSetName, componentName, basePoint, wcsMatrix, layer, out partLoadStatus);
                            lw.WriteLine($"Part Loaded: {componentName}");
                        }
                        workPart.Save(BasePart.SaveComponents.False, BasePart.CloseAfterSave.False);
                    }
                }
            }
            catch (NXException e)
            {
                theUI.NXMessageBox.Show("Exception", NXMessageBox.DialogType.Error, e.Message.ToString());
            }
        }
        public void CreateLine()
        {
            Point3d startPoint = new Point3d(0, 0, 0);
            Point3d endPoint = new Point3d(0, 100, 100);
            line = workPart.Curves.CreateLine(startPoint, endPoint);
            line.SetVisibility(SmartObject.VisibilityOption.Visible);
        }

        public void CreatePointPerpendicularToLine()
        {
            double[] startPoint = { line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z };
            double[] endPoint = { line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z };

            double[] midPt = {
                (startPoint[0] + endPoint[0]) / 2,
                (startPoint[1] + endPoint[1]) / 2,
                (startPoint[2] + endPoint[2]) / 2
            };

            //Point midPoint = workPart.Points.CreatePoint(new Point3d(
            //(line.EndPoint.X + line.StartPoint.X) / 2,
            //(line.EndPoint.Y + line.StartPoint.Y) / 2,
            //(line.EndPoint.Z + line.StartPoint.Z) / 2)
            //);

            Point midPoint = workPart.Points.CreatePoint(new Point3d(midPt[0], midPt[1], midPt[2]));
            midPoint.SetVisibility(SmartObject.VisibilityOption.Visible);

            // Calculate the direction vector of the line
            double[] lineVector = {
                (line.EndPoint.X - line.StartPoint.X),
                (line.EndPoint.Y - line.StartPoint.Y),
                (line.EndPoint.Z - line.StartPoint.Z)
            };

            //The direction vector we calculated above has a magnitude (or length) based on the distance between the start and end points.
            //However, to work with directions without scaling, we often use a unit vector—a vector with a magnitude of 1 that points in the same direction.
            //to get unit vector, divide each component of vector with length
            double length = Math.Sqrt(
                (lineVector[0] * lineVector[0]) +
                (lineVector[1] * lineVector[1]) +
                (lineVector[2] * lineVector[2])
            );

            double[] unitVector = {
                lineVector[0]/length,
                lineVector[1]/length,
                lineVector[2]/length,
            };

            //Let us assume that it is in XY plane to reduce complexity, now the perpendicular to (X,Y,0) will be(-Y,X,0)
            //If we need it in YZ plane, perpendicular to (0,Y,Z) will be (0,-Z,Y)
            double[] perpendicularVector = {
                -unitVector[1],
                unitVector[0],
                0
            };

            //Move the mid point perpendicular direction with distance 10
            double distance = 10;
            double[] movedPt = {
                midPt[0]+perpendicularVector[0]*distance,
                midPt[1]+perpendicularVector[1]*distance,
                midPt[2]+perpendicularVector[2]*distance
            };

            Point movedPoint = workPart.Points.CreatePoint(new Point3d(movedPt[0], movedPt[1], movedPt[2]));
            movedPoint.SetVisibility(SmartObject.VisibilityOption.Visible);

            CreatingPlanes(movedPoint);
        }

        public void CreateLinePerpendicularToExistingLine()
        {

            //In CreatePointPerpendicularToLine method we used double[] array to store coordinates and vectors,
            //so here we try to use point3d and vector3d to store coordinates and vectors

            //Calculating Mid Point from start and end points, by adding and dividing the corresponding components by 2
            Point3d startPoint = line.StartPoint;
            Point3d endPoint = line.EndPoint;

            Point3d midPoint = new Point3d(
                (startPoint.X + endPoint.X) / 2,
                (startPoint.Y + endPoint.Y) / 2,
                (startPoint.Z + endPoint.Z) / 2
            );

            //calculating direction vector of original line, we can use double[] array or vector3D to store the direction vector
            Vector3d directionVectorOfOriginalLine = new Vector3d(
                endPoint.X - startPoint.X,
                endPoint.Y - startPoint.Y,
                endPoint.Z - startPoint.Z

            );

            //Calculate magnitude or length of the original line to calculate unit vector
            double length = Math.Sqrt(
                (directionVectorOfOriginalLine.X * directionVectorOfOriginalLine.X) +
                (directionVectorOfOriginalLine.Y * directionVectorOfOriginalLine.Y) +
                (directionVectorOfOriginalLine.Z * directionVectorOfOriginalLine.Z)
            );

            //unit vector of original line
            Vector3d unitVectorOfOriginalLine = new Vector3d(
                directionVectorOfOriginalLine.X / length,
                directionVectorOfOriginalLine.Y / length,
                directionVectorOfOriginalLine.Z / length
            );

            //Perpendicuar direction vector, let us assume it on YZ plane,
            //perpendicular direction vector for (0,Y,Z) is (0,-Z,Y)
            Vector3d unitVectorOfNewLine = new Vector3d(
                0,
                -unitVectorOfOriginalLine.Z,
                unitVectorOfOriginalLine.Y
            );

            //Perpendicular end point for new line
            double lengthOfNewLine = 30;
            Point3d perpendicularEndPointForNewLine = new Point3d(
                midPoint.X + unitVectorOfNewLine.X * lengthOfNewLine,
                midPoint.Y + unitVectorOfNewLine.Y * lengthOfNewLine,
                midPoint.Z + unitVectorOfNewLine.Z * lengthOfNewLine
            );

            Line newPerpendicularLine = workPart.Curves.CreateLine(midPoint, perpendicularEndPointForNewLine);
        }

        public void CreatingPlanes(Point startPoint)
        {
            //For creating a plane we need origin(Point3d) and Matrix3X3
            //Creating plane along XY Plane normal to Z axis
            //In this case Matrix components will be ->Xaxis(1,0,0)    Yaxis(0,1,0)    Zaxis(0,0,1)
            //along YZ plane Normal to X axis       -> Xaxis(0,0,1)    Yaxis(0,1,0)    Zaxis(1,0,0)
            //along ZX plane Normal to Y axis       -> Xaxis(1,0,0)    Yaxis(0,0,1)    Zaxis(0,1,0)

            //Lets create a plane via a point which is create on above method, along XY plane by taking workPart matrix
            Matrix3x3 wcsMatrix = workPart.WCS.CoordinateSystem.Orientation.Element;  //This will collect Matrix along XY
            Point3d origin = new Point3d(startPoint.Coordinates.X, startPoint.Coordinates.Y, startPoint.Coordinates.Z);
            DatumPlane sketchPlane = workPart.Datums.CreateFixedDatumPlane(origin, wcsMatrix);

            lw.WriteLine(("Xx = " + wcsMatrix.Xx, "Xy = " + wcsMatrix.Xy, "Xz = " + wcsMatrix.Xz).ToString());
            lw.WriteLine(("Yx = " + wcsMatrix.Yx, "Yy = " + wcsMatrix.Yy, "Yz = " + wcsMatrix.Yz).ToString());
            lw.WriteLine(("Zx = " + wcsMatrix.Zx, "Zy = " + wcsMatrix.Zy, "Zz = " + wcsMatrix.Zz).ToString());

            //Creating plane via point, along XZ axis and normal to Y
            Matrix3x3 xzMatrix = new Matrix3x3();
            xzMatrix.Xx = 1; xzMatrix.Xy = 0; xzMatrix.Xz = 0;
            xzMatrix.Yx = 0; xzMatrix.Yy = 0; xzMatrix.Yz = 1;
            xzMatrix.Zx = 0; xzMatrix.Zy = 1; xzMatrix.Zz = 0;

            DatumPlane xzPlane = workPart.Datums.CreateFixedDatumPlane(origin, xzMatrix);


            //Creating a plane with some angle
            // Calculate components of the normal vector
            //CreateFixedDatumPlane:
            // Use when you need a persistent and reusable reference plane for building features.
            //Example: Creating a sketch plane that stays in the part's feature tree.
            //CreateFixedPlane:
            // Use when you need a temporary plane for programmatic calculations or intermediate steps.
            //Example: Finding where a plane intersects a body without adding a datum plane to the model.

            Point3d originOriginal = new Point3d(0, 0, 0);
            double angleInRadians = 53 * (Math.PI / 180.0); // Convert degrees to radians
            double normalZ = Math.Sin(angleInRadians); // Z-component
            double normalXY = Math.Cos(angleInRadians); // Length in XY plane

            // 1. Plane normal to Z (tilted 53 degrees to XY)
            Matrix3x3 matrixZ = new Matrix3x3();
            matrixZ.Xx = normalXY; matrixZ.Xy = 0.0; matrixZ.Xz = -normalZ; // X-axis direction
            matrixZ.Yx = 0.0; matrixZ.Yy = 1.0; matrixZ.Yz = 0.0;          // Y-axis direction
            matrixZ.Zx = normalZ; matrixZ.Zy = 0.0; matrixZ.Zz = normalXY; // Z-axis direction (normal)

            Plane planeZ = workPart.Planes.CreateFixedPlane(originOriginal, matrixZ);

            // 2. Plane normal to X (tilted 53 degrees to YZ)
            Matrix3x3 matrixX = new Matrix3x3();
            matrixX.Xx = 1.0; matrixX.Xy = 0.0; matrixX.Xz = 0.0;          // X-axis direction
            matrixX.Yx = 0.0; matrixX.Yy = normalXY; matrixX.Yz = normalZ; // Y-axis direction
            matrixX.Zx = 0.0; matrixX.Zy = -normalZ; matrixX.Zz = normalXY; // Z-axis direction (normal)

            Plane planeX = workPart.Planes.CreateFixedPlane(originOriginal, matrixX);

            // 3. Plane normal to Y (tilted 53 degrees to ZX)
            Matrix3x3 matrixY = new Matrix3x3();
            matrixY.Xx = normalXY; matrixY.Xy = normalZ; matrixY.Xz = 0.0; // X-axis direction
            matrixY.Yx = -normalZ; matrixY.Yy = normalXY; matrixY.Yz = 0.0; // Y-axis direction
            matrixY.Zx = 0.0; matrixY.Zy = 0.0; matrixY.Zz = 1.0;          // Z-axis direction (normal)

            Plane planeY = workPart.Planes.CreateFixedPlane(originOriginal, matrixY);

            SketchInPlaceBuilder sketchInPlaceBuilder = workPart.Sketches.CreateSketchInPlaceBuilder2(null);

            //sketchInPlaceBuilder.plan = sketchPlane;
            //sketchInPlaceBuilder.PlaneReference.Direction = Sketch.Direction.Normal; // Sketch normal to the plane

            //Sketch sketch = sketchInPlaceBuilder.Commit();
            //sketchInPlaceBuilder.Destroy();

            //// Step 3: Add Geometry to the Sketch
            //sketch.Open(); // Enter sketch editing mode

            //Sketch.LineBuilder lineBuilder = sketch.CreateLine();
            //Point3d startPoint = new Point3d(0.0, 0.0, 0.0);
            //Point3d endPoint = new Point3d(100.0, 0.0, 0.0);
            //lineBuilder.StartPoint = startPoint;
            //lineBuilder.EndPoint = endPoint;
            //lineBuilder.Commit();

            //sketch.Close(); // Exit sketch editing mode

            //// Finalize
            //theSession.UpdateManager.DoUpdate(new Session.UpdateHint[] { });

        }

        public void GetDirections()
        {
            int faceType;
            double[] pointInfo = new double[3];
            double[] direction = new double[3];
            double[] box = new double[6];
            double radius;
            double radData;
            int normDir;
            theUFSession.Modl.AskFaceData(lsFaces[8].Tag, out faceType, pointInfo, direction, box, out radius, out radData, out normDir);

            lsFaces[8].Highlight();

            lw.WriteLine(Convert.ToString(direction[0]));
            lw.WriteLine(Convert.ToString(direction[1]));
            lw.WriteLine(Convert.ToString(direction[2]));

            CreateAPointAsFeature(pointInfo[0], pointInfo[1], pointInfo[2]);

            lw.WriteLine("Point");

            lw.WriteLine(Convert.ToString(pointInfo[0]));
            lw.WriteLine(Convert.ToString(pointInfo[1]));
            lw.WriteLine(Convert.ToString(pointInfo[2]));
        }

        public void GetCGFromFace()
        {

        }

        public void CreateAPointAsFeature(double x,double y,double z)
        {

            NXOpen.Point point6; 
            point6 =workPart.Points.CreatePoint(new Point3d(x,y,z)); 

            point6.SetVisibility(NXOpen.SmartObject.VisibilityOption.Visible); 
            NXOpen.Features.Feature nullNXOpen_Features_Feature= null;
            NXOpen.Features.PointFeatureBuilder pointFeatureBuilder1;
            pointFeatureBuilder1 = workPart.BaseFeatures.CreatePointFeatureBuilder(nullNXOpen_Features_Feature); 

            pointFeatureBuilder1.Point=point6; 
            NXOpen.NXObject nXObject1; 
            nXObject1 = pointFeatureBuilder1.Commit(); 
            pointFeatureBuilder1.Destroy();
        }

        public void SegrigateCurves()
        {
            //We can use UF API to do this such as
            //UF_EVAL_initialize
            //UF_EVAL_is_arc
            //UF_EVAL_is_ellipse

            List<Line> lines = new List<Line>();
            List<Arc> arcs = new List<Arc>();
            List<Arc> circles = new List<Arc>();
            List<Spline> splines = new List<Spline>();
            List<Curve> otherCurves = new List<Curve>();

            Curve[] curves = workPart.Curves.ToArray();

            foreach (Curve curve in curves) 
            {
                switch (curve.GetType().ToString())
                {
                    case "NXOpen.Line":
                        lines.Add((Line)curve);
                        break;
                    case "NXOpen.Arc":
                        if (Math.Abs(((Arc)curve).EndAngle - ((Arc)curve).StartAngle) >= (2 * Math.PI))
                        {
                            circles.Add((Arc)curve);
                        }
                        else
                        {
                            arcs.Add((Arc)curve);
                        }
                        break;
                    case "NXOpen.Spline":
                        splines.Add((Spline)curve);
                        break;

                    default:
                        otherCurves.Add(curve);
                        break;
                }
            }

            lw.WriteLine($"Lines: {lines.Count}");
            lw.WriteLine($"Arcs: {arcs.Count}");
            lw.WriteLine($"Circles: {circles.Count}");
            lw.WriteLine($"Splines: {splines.Count}");
            lw.WriteLine($"Others: {otherCurves.Count}");

            //to find length of line, center point or radius of arc or circle
            IntPtr evalu;
            theUFSession.Eval.Initialize(curves[0].Tag, out evalu);
            if (curves[0] is Line) 
            {
                UFEval.Line line;
                theUFSession.Eval.AskLine(evalu, out line);
                double d=line.length;
                lw.WriteLine($"Length of line: {d}");
            }
        }

        public void GetSpecificComponentFromNamedFace()
        {
            string faceName = "RECFACE";
            Component reqComponent;
            List<Body> bodies;
            List<Face> faces;
            GetBodiesAndFacesFromAssembly(out bodies,out faces);

            foreach (Face face in faces)
            {
                if (face.Name.ToUpper() == faceName)
                {
                    Body reqBody=face.GetBody();
                    reqComponent = reqBody.OwningComponent;
                }
            }
        }
    }
}
