# DbTest.Containers
library for testing using Database containers

# about me 2 05:00

# plan 3
0
# why ? 4 (mem)

# definition 5

# как строим подход - через кейсы 6

# пример бизнес процесса - 7 10:00

# 20 15:00

# 30 20:00

# 40 31:00

# 50 38:00

# 60 43:00

# summary - 70 55:00

1. Как можно тестировать базу данных?

Почему возникла идея сделать доклад? Кажется почти все сталкивались с задачей тестирования на "настоящих" базах данных. Но я не видел никаких подробных докладов на этот счет.

2. Какие тесты лучше выбрать для какой задачи?

Мы не будем говорить о чистых тестах. На практике

3. Обзор существующих решений и рекоммендации от Microsoft по тестированию EF.

4. Strategies for isolating the database in tests

Одним из ключевых условий наличия поддерживаемых тестов является обеспечение того, чтобы тесты были изолированными и воспроизводимыми. Для модульных тестов это несложно, если мы держимся подальше от глобальных переменных, статических классов и общего глобального состояния. Это становится некоторой проблемой при интеграционных тестах, которые взаимодействуют с базой данных, где состояние по определению является глобальным и общим. Чтобы иметь поддерживаемый набор интеграционных тестов, нам нужно убедиться, что наши тесты всегда имеют согласованную отправную точку.

5. Тестовая база данных для каждого разработчика
Одна ошибка, которую, как я вижу, допускают многие команды, заключается в неправильной изоляции баз данных для каждого разработчика. Я часто вижу один общий экземпляр базы данных разработчиков для каждой команды:

Это затрудняет тестирование и разработку для обоих участников, поскольку не только у меня есть общее состояние на моей собственной машине, но и другие люди могут изменять данные из-под моего контроля. Не самое подходящее место для того, чтобы находиться в нем! Мы можем пойти еще дальше и создать локальную базу данных разработчиков для каждого разработчика:

Теперь каждый разработчик может безопасно вносить изменения (вы выполняете миграцию базы данных, верно?) в свою локальную базу данных, не беспокоясь о вмешательстве других разработчиков. Но мы хотим сделать еще один шаг вперед и создать отдельную локальную базу данных для разработки и тестирования:

Наша база данных разработчиков проходит миграцию схемы, но никогда не “стирается начисто”. Его данные остаются неизменными, так что во время разработки нам не нужно начинать с пустой базы данных. Кроме того, если для разработки требуется много данных в нашей базе данных разработчиков, мы все равно можем сохранить их для нормальной разработки.

Для тестирования, где нам нужны детерминированные условия настройки, тестовая база данных используется только для автоматического тестирования и поддерживается в известном состоянии перед каждым тестом. Нам нужно только настроить нашу стратегию миграции, чтобы поддерживать обе локальные базы данных в актуальном состоянии локально (но это не должно быть проблемой с современными инструментами миграции).

Теперь, когда у нас есть соответствующая локальная настройка, давайте рассмотрим некоторые варианты поддержания нашей тестовой базы данных в надежно согласованном состоянии.

5. Откат транзакций
Один из самых простых способов отката изменений, внесенных во время теста, - это... откат изменений, внесенных во время теста. Мы открываем транзакцию в начале теста, выполняем некоторую работу, а в конце теста откатываем эту транзакцию.

Поскольку базы данных (в зависимости от нашего уровня изоляции) включают изменения, внесенные нами внутри транзакции, с последующим чтением, наши тесты все равно могут запрашивать сделанные обновления. А затем, в конце теста, наши изменения исчезают.

В нашем тесте мы можем использовать расширения setup/teardown или before/after test для открытия внешней транзакции и последующего ее отката. Однако, чтобы это работало должным образом, наши базовые подключения к данным / ORM должны быть осведомлены об окружающих транзакциях. xUnit.net включает в себя простое расширение для этого с атрибутом AutoRollback в наших тестах.

Одним из побочных эффектов этого является то, что, поскольку наша транзакция автоматически откатывается, если нам нужно отладить наши тестовые данные после запуска теста, мы не можем этого сделать, поскольку данные исчезли.

Кроме того, если нам по какой-либо причине потребуется несколько транзакций, этот подход не сработает. Иногда я работаю с системами, которые имеют одно действие с несколькими внутренними транзакциями, все из которых могут быть идемпотентными или могут выполняться несколько раз, не влияя на один тест, но не откатываются.

Если простого отката будет недостаточно, нам нужно рассмотреть возможность простой очистки базы данных перед каждым тестом.

6. Сброс базы данных перед каждым тестом
Вот тут-то все и становится интересным, потому что в большинстве баз данных нет никакого переключателя “сброс”. Вместо этого мы должны разработать интересные способы удаления данных приложения из базы данных перед запуском каждого теста. Я видел несколько способов сделать это, в том числе:

Отсоединение базы данных и восстановление заведомо “хорошей” резервной копии
Отключение всех ограничений FK, усечение каждой таблицы и восстановление ограничений FK
Найдите “правильный” порядок удаления данных на основе взаимосвязей и удалите данные из каждой таблицы по порядку

Первый способ действительно работает только в том случае, если моя база данных меняется не часто. Сохранение хорошей резервной копии, которую можно эффективно восстановить, раздражает, если схема меняется, и я должен поддерживать эту резервную копию в актуальном состоянии. Это действительно полезно только в случае использования производственной базы данных в качестве моей тестовой базы данных, но в остальном это сплошная боль.

Следующий вариант включает в себя просто очистку каждой таблицы, которую я нахожу в нашей базе данных, независимо от порядка. Чтобы обойти нарушения ограничений, я могу отключить все ограничения, обрезать каждую таблицу, а затем восстановить ограничения. Проблема с этим подходом заключается в том, что он довольно медленный, с 3 командами базы данных на таблицу.

Наконец, вариант, который я предпочитаю, - это изучить метаданные SQL для построения графика таблиц и взаимосвязей. Если я удаляю данные при обходе в глубину, это гарантирует, что я не нарушу ограничения и FK при удалении объектов.

7. Respawn от J.Bogard

Общая проблема заключается в попытке найти правильный порядок удаления для таблиц, когда у вас есть ограничения внешнего ключа. Вы можете сделать что-то вроде:
Вы просто игнорируете, в каком порядке вы можете удалять объекты, просто отключив все ограничения, удалив, а затем повторно включив. К сожалению, это приводит к увеличению количества SQL-инструкций в 3 раза по сравнению с just DELETE, что замедляет весь ваш тестовый запуск. Respawn исправляет это, разумно составляя список УДАЛЕНИЙ, а с 3.0 обнаруживая циклические взаимосвязи. Но как?

Обход графика
Предполагая, что мы правильно настроили ограничения внешнего ключа, мы можем просмотреть нашу схему через ее взаимосвязи:
Другой способ подумать об этом - представить каждую таблицу как узел, а каждый внешний ключ - как ребро. Но это не просто какой-то край, это направление. Собрав все это вместе, мы можем построить ориентированный граф:

Существует особый вид графа, ориентированный ациклический граф, где нет циклов, но мы не можем сделать такого предположения. Мы знаем только, что существуют направленные ребра.

Итак, зачем нам нужен этот ориентированный граф? Предполагая, что у нас нет никаких каскадов, настроенных для наших внешних ключей, порядок, в котором мы удаляем таблицы, должен начинаться с таблиц без внешних ключей, затем таблиц, которые ссылаются на них, затем таблиц, которые ссылаются на них, и так далее, пока мы не дойдем до последней таблицы. Таблицы, которые мы удаляем первыми, - это те, на которые не указывают внешние ключи, потому что от них не зависят никакие таблицы.

В терминах ориентированного графа это известно как поиск в глубину. Упорядоченный список таблиц находится путем выполнения поиска в глубину, добавляя узлы в самом глубоком начале, пока мы не достигнем корня (ов) графика. Когда мы закончим, список для удаления - это просто перевернутый список узлов. По мере прохождения мы отслеживаем посещенные узлы, исчерпывая наш список не посещенных, пока он не опустеет:

Работа с циклами
Существует множество литературы и примеров по обнаружению циклов в ориентированном графе, но мне нужно не просто обнаружить цикл. Я должен понять, что мне следует делать, когда я обнаруживаю цикл? Как это должно повлиять на общую стратегию удаления?

Для этого все действительно зависит от базы данных. Общая проблема заключается в том, что ограничения внешнего ключа не позволяют нам удалять с помощью сирот, но с циклом в нашем графике я не могу удалять таблицы одну за другой.

8. EfCore.TestSupport J.P.Smith 
Существует три основных способа автоматизировать тестирование вашего основного кода EF:
Используйте базу данных того же типа, что и ваша производственная система. Лучший выбор
Используйте базы данных SQLite в памяти. Самый быстрый выбор, но имеет ограничения
Используйте какую-либо форму шаблона репозитория и имитируйте свой репозиторий. Хорошо, но больше работы.
Если вы используете базу данных, вам необходимо создать уникальную пустую тестовую базу данных с правильной схемой. Я описываю три способа сделать это.
Существует базовая функция EF, называемая разрешением идентификационных данных, которая может привести к тому, что код с ошибками все еще будет…

9. Testcontainers-dotnet
Реализация тестов в докере при помощи Docker.DotNet
Нюансы, возникающие в CICD
Технологии
Entity Framework Core
Respawn
EfCore.TestSupport
Testcontainers-dotnet
Docker.DotNet

references
https://lostechies.com/jimmybogard/2013/06/18/strategies-for-isolating-the-database-in-tests/
