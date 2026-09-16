# Web

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.1.8.

## Development server

This app is normally served as part of `docker compose up` from the repo root (service `web`, see the top-level README) — a production build behind nginx, no manual step needed, restarts automatically with the rest of the stack. There's no hot-reload there though: a code change needs `docker compose up -d --build web`.

For live-reload during active development, run the dev server standalone instead (outside Docker) — do NOT also have the `web` container running, both bind to port 4201:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4201/` (port is pinned in `angular.json` — `4200` collides with an unrelated local Docker project). The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
