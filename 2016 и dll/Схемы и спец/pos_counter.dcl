// ===========================================
// POS COUNTER v4.0 - Universal Dialog
// For AutoCAD 2016-2026
// ===========================================

pos_counter : dialog {
    label = "POS COUNTER v4.0";
    key = "title";
    initial_focus = "layer_list";
    spacer;

    : row {
        : radio_button {
            key = "view_dwg";
            label = "By DWG";
            value = "1";
            width = 15;
        }
        : radio_button {
            key = "view_viewport";
            label = "By viewport";
            value = "0";
            width = 20;
        }
    }

    : row {
        : radio_button {
            key = "mode_layer";
            label = "By layer";
            value = "1";
            width = 15;
        }
        : radio_button {
            key = "mode_selection";
            label = "By selection";
            value = "0";
            width = 20;
        }
    }

    : row {
        : radio_button {
            key = "scope_current";
            label = "Only this layer";
            value = "1";
            width = 20;
        }
        : radio_button {
            key = "scope_all";
            label = "All layers";
            value = "0";
            width = 15;
        }
    }

    spacer;

    : row {
        : text {
            label = "Layer for counting:";
            width = 20;
        }
        : popup_list {
            key = "layer_list";
            width = 30;
        }
    }

    spacer;

    : boxed_column {
        label = "COUNTING RESULTS:";
        : list_box {
            key = "results_box";
            width = 50;
            height = 16;
        }
    }

    spacer;

    : row {
        fixed_width = true;
        alignment = centered;
        : button {
            key = "btn_count";
            label = "COUNT";
            width = 15;
        }
        : spacer { width = 2; }
        : button {
            key = "btn_export";
            label = "EXPORT TO EXCEL";
            width = 20;
        }
        : spacer { width = 2; }
        : button {
            key = "cancel";
            label = "CLOSE";
            is_cancel = true;
            width = 10;
        }
    }

    : row {
        fixed_width = true;
        alignment = centered;
        : button {
            key = "btn_show";
            label = "SHOW ON DRAWING";
            width = 20;
        }
    }

    spacer;

    : text {
        key = "status";
        label = "Ready. Mode: by layer. Select layer and press COUNT";
        width = 60;
    }
}

