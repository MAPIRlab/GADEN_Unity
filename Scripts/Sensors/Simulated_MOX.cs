using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulated_MOX : MonoBehaviour
{

    public int input_sensor_model=1;
    public GameObject gadenPlayer;


    bool first_reading=true;
    float sensor_output;                //MOX model response
    float previous_sensor_output;       //The response in (t-1)

    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------

    // MOX sensitivity. Extracted from datasheets and curve fitting
    //--------------------------------------------------------------
    float[] Sensitivity_Air = new float[]{21, 1, 8.8f, 10.3f, 19.5f};      //RS/R0 when exposed to clean air (datasheet)
    float[] R0 = new float[]{3000, 50000, 3740, 3740, 4500};      //[Ohms] Reference resistance (see datasheets)
    // RS/R0 = A*conc^B (a line in the loglog scale)
    float[][][] sensitivity_lineloglog=new float[][][]{   //5 Sensors, 7 Gases, 2 Constants: A, B
    new float[][]{  //TGS2620    
        new float[] {62.32f, -0.7155f},   //Ethanol
        new float[] {120.6f, -0.4877f},   //Methane
        new float[] {24.45f, -0.5546f},   //Hydrogen
        new float[] {120.6f, -0.4877f},   //propanol (To review)
        new float[] {120.6f, -0.4877f},   //chlorine (To review)
        new float[] {120.6f, -0.4877f},   //fluorine (To review)
        new float[] {120.6f, -0.4877f}    //Acetone (To review)
    },

        new float[][]{  //TGS2600    
        new float[] {0.6796f, -0.3196f},   //ethanol
        new float[] {1.018f, -0.07284f},   //methane
        new float[] {0.6821f, -0.3532f},    //hydrogen
        new float[] {1.018f, -0.07284f},   //propanol (To review)
        new float[] {1.018f, -0.07284f},   //chlorine (To review)
        new float[] {1.018f, -0.07284f},   //fluorine (To review)
        new float[] {1.018f, -0.07284f}    //Acetone (To review)
    },

    new float[][]{  //TGS2611    
        new float[] {51.11f, -0.3658f},    //ethanol
        new float[] {38.46f, -0.4289f},    //methane
        new float[] {41.3f, -0.3614f},     //hydrogen
        new float[] {38.46f, -0.4289f},   //propanol (To review)
        new float[] {38.46f, -0.4289f},   //chlorine (To review)
        new float[] {38.46f, -0.4289f},   //fluorine (To review)
        new float[] {38.46f, -0.4289f}    //Acetone (To review)
    },

    new float[][]{  //TGS2610
        new float[] {106.1f, -0.5008f},     //ethanol
        new float[] {63.91f, -0.5372f},     //methane
        new float[] {66.78f, -0.4888f},     //hydrogen
        new float[] {63.91f, -0.5372f},   //propanol (To review)
        new float[] {63.91f, -0.5372f},   //chlorine (To review)
        new float[] {63.91f, -0.5372f},   //fluorine (To review)
        new float[] {63.91f, -0.5372f}    //Acetone (To review)
    },

    new float[][]{  //TGS2612
        new float[] {31.35f, -0.09115f},   //ethanol
        new float[] {146.2f, -0.5916f},    //methane
        new float[] {19.5f, 0.0f},         //hydrogen
        new float[] {146.2f, -0.5916f},   //propanol (To review)
        new float[] {146.2f, -0.5916f},   //chlorine (To review)
        new float[] {146.2f, -0.5916f},   //fluorine (To review)
        new float[] {146.2f, -0.5916f}    //Acetone (To review)
    }
};

    //Time constants (Rise, Decay)
    float[][][] tau_value =   new float[][][]   //5 sensors, 7 gases , 2 Time Constants
    {
        new float[][]{  //TGS2620
        new float[]{2.96f, 15.71f},  //ethanol
        new float[]{2.96f, 15.71f},  //methane
        new float[]{2.96f, 15.71f},  //hydrogen
        new float[]{2.96f, 15.71f},  //propanol
        new float[]{2.96f, 15.71f},  //chlorine
        new float[]{2.96f, 15.71f},  //fluorine
        new float[]{2.96f, 15.71f}   //Acetone
        },

        new float[][]{  //TGS2600
        new float[]{4.8f, 18.75f},   //ethanol
        new float[]{4.8f, 18.75f},   //methane
        new float[]{4.8f, 18.75f},   //hydrogen
        new float[]{4.8f, 18.75f},   //propanol
        new float[]{4.8f, 18.75f},   //chlorine
        new float[]{4.8f, 18.75f},   //fluorine
        new float[]{4.8f, 18.75f}    //Acetone
        },

        new float[][]{  //TGS2611
        new float[]{3.44f, 6.35f},   //ethanol
        new float[]{3.44f, 6.35f},   //methane
        new float[]{3.44f, 6.35f},   //hydrogen
        new float[]{3.44f, 6.35f},   //propanol
        new float[]{3.44f, 6.35f},   //chlorine
        new float[]{3.44f, 6.35f},   //fluorine
        new float[]{3.44f, 6.35f}    //Acetone
        },

        new float[][]{  //TGS2610
        new float[]{3.44f, 6.35f},   //ethanol
        new float[]{3.44f, 6.35f},   //methane
        new float[]{3.44f, 6.35f},   //hydrogen
        new float[]{3.44f, 6.35f},   //propanol
        new float[]{3.44f, 6.35f},   //chlorine
        new float[]{3.44f, 6.35f},   //fluorine
        new float[]{3.44f, 6.35f}    //Acetone
        },

        new float[][]{  //TGS2612
        new float[]{3.44f, 6.35f},   //ethanol
        new float[]{3.44f, 6.35f},   //methane
        new float[]{3.44f, 6.35f},   //hydrogen
        new float[]{3.44f, 6.35f},   //propanol
        new float[]{3.44f, 6.35f},   //chlorine
        new float[]{3.44f, 6.35f},   //fluorine
        new float[]{3.44f, 6.35f}    //Acetone
        }
    };
    
    
    
    
    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------
    //------------------------------------------------------------------------------
    
    
    
    float getMeasurement(Vector3 request){

        GasMeasurement response=gadenPlayer.GetComponent<GADEN_player>().getConcentration(request);
        return simulate_mox_as_line_loglog(response);
    }

    float simulate_mox_as_line_loglog(GasMeasurement gasMeasurement)
    {
        if (first_reading)
        {
            //Init sensor to its Baseline lvl
            sensor_output = Sensitivity_Air[input_sensor_model];    //RS_R0 value at air
            previous_sensor_output = sensor_output;
            first_reading = false;
            // Return Sensor response for current time instant as the Sensor Resistance in Ohms
            return (sensor_output * R0[input_sensor_model]);
        }   
        //1. Set Sensor Output based on gas concentrations (gas type dependent)
        //---------------------------------------------------------------------
        // RS/R0 = A*conc^B (a line in the loglog scale)
        float resistance_variation = 0;

        int gas_id;
        if (gasMeasurement.gas_type == "ethanol")
            gas_id = 0;
        else if (gasMeasurement.gas_type == "methane")
            gas_id = 1;
        else if (gasMeasurement.gas_type == "hydrogen")
            gas_id = 2;
        else if (gasMeasurement.gas_type == "propanol")
            gas_id = 3;
        else if (gasMeasurement.gas_type == "chlorine")
            gas_id = 4;
        else if (gasMeasurement.gas_type == "fluorine")
            gas_id = 5;
        else if (gasMeasurement.gas_type == "acetone")
            gas_id = 6;
        else
        {
            Debug.Log("[fake_mox] MOX response is not configured for this gas type!");
            return 0.0f;
        }

        //Value of RS/R0 for the given gas and concentration
        float RS_R0 = sensitivity_lineloglog[input_sensor_model][gas_id][0] * Mathf.Pow(gasMeasurement.ppm, sensitivity_lineloglog[input_sensor_model][gas_id][1]);

        //Ensure we never overpass the baseline level (max allowed)
        if (RS_R0 > Sensitivity_Air[input_sensor_model])
            RS_R0= Sensitivity_Air[input_sensor_model];

        //Increment with respect the Baseline
        resistance_variation += Sensitivity_Air[input_sensor_model] - RS_R0;

        //Calculate final RS_R0 given the final resistance variation
        RS_R0 = Sensitivity_Air[input_sensor_model] - resistance_variation;

        //Ensure a minimum sensor resitance
        if (RS_R0 <= 0.0f)
            RS_R0 = 0.01f;



        //2. Simulate transient response (dynamic behaviour, tau_r and tau_d)
        //---------------------------------------------------------------------
        float tau;
        if (RS_R0 < previous_sensor_output)  //rise
            tau = tau_value[input_sensor_model][0][0];
        else //decay
            tau = tau_value[input_sensor_model][0][1];

        // Use a low pass filter
        //alpha value = At/(tau+At)
        float alpha = Time.deltaTime / (tau+Time.deltaTime);

        //filtered response (uses previous estimation):
        sensor_output = (alpha*RS_R0) + (1-alpha)*previous_sensor_output;

        //Update values
        previous_sensor_output = sensor_output;

        // Return Sensor response for current time instant as the Sensor Resistance in Ohms
        return (sensor_output * R0[input_sensor_model]);
    }

    
}
