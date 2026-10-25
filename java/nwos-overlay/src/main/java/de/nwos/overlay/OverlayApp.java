package de.nwos.overlay;

import de.nwos.contracts.ModuleMessage;
import de.nwos.contracts.ModuleMessageOutcome;
import de.nwos.contracts.PermissionScope;
import de.nwos.ipc.IpcClient;
import de.nwos.ipc.IpcDefaults;
import javafx.application.Application;
import javafx.application.Platform;
import javafx.geometry.Point2D;
import javafx.scene.Scene;
import javafx.scene.control.Label;
import javafx.scene.input.MouseEvent;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.stage.Stage;
import javafx.stage.StageStyle;

import java.time.Duration;

import java.util.Set;

/**
 * VPT3-Overlay: transparente, immer im Vordergrund liegende Leiste - der
 * Ersatz für die aufgegebene, defekte 48px-WPF-Band-Implementierung
 * (NWOS.DesktopShell.Ui) ohne jede .NET-Abhängigkeit.
 *
 * <p>Die Leiste ist per Drag verschiebbar und fragt beim Start den
 * Cardinal-Status über den IPC-Bus ab (Identität: nwos.overlay).</p>
 */
public class OverlayApp extends Application {

    private static final String OVERLAY_IDENTITY = IpcDefaults.EXTERNAL_OVERLAY_IDENTITY;

    private final Label statusLabel = new Label("Verbinde mit Cardinal ...");

    @Override
    public void start(Stage stage) {
        var title = new Label("NWOS");
        title.getStyleClass().add("brand");

        var filler = new Region();
        HBox.setHgrow(filler, Priority.ALWAYS);

        var bar = new HBox(12, title, filler, statusLabel);
        bar.getStyleClass().add("bar");

        var scene = new Scene(bar, 420, 44);
        scene.setFill(null);
        scene.getStylesheets().add(getClass().getResource("/overlay.css").toExternalForm());

        makeDraggable(stage, bar);

        stage.initStyle(StageStyle.TRANSPARENT);
        stage.setAlwaysOnTop(true);
        stage.setTitle("NWOS Overlay");
        stage.setScene(scene);
        stage.show();

        Thread.ofVirtual().name("nwos-overlay-cardinal-link").start(this::queryCardinalStatus);
    }

    private void queryCardinalStatus() {
        try (var client = new IpcClient(OVERLAY_IDENTITY, IpcDefaults.DEFAULT_PORT, Duration.ofSeconds(3))) {
            client.connect();
            var message = ModuleMessage.create(OVERLAY_IDENTITY, "nwos.system", "system.status", "{}",
                    Set.of(PermissionScope.FILE_SYSTEM_READ));
            var result = client.send(message);
            var text = result.outcome() == ModuleMessageOutcome.DELIVERED
                    ? "Cardinal: aktiv"
                    : "Cardinal: " + result.outcome() + (result.reason() == null ? "" : " (" + result.reason() + ")");
            Platform.runLater(() -> statusLabel.setText(text));
        } catch (Exception ex) {
            Platform.runLater(() -> statusLabel.setText("Cardinal nicht erreichbar"));
        }
    }

    private void makeDraggable(Stage stage, HBox bar) {
        final Point2D[] dragOffset = new Point2D[1];
        bar.addEventHandler(MouseEvent.MOUSE_PRESSED, event ->
                dragOffset[0] = new Point2D(event.getSceneX(), event.getSceneY()));
        bar.addEventHandler(MouseEvent.MOUSE_DRAGGED, event -> {
            stage.setX(event.getScreenX() - dragOffset[0].getX());
            stage.setY(event.getScreenY() - dragOffset[0].getY());
        });
    }

    public static void main(String[] args) {
        launch(args);
    }
}
